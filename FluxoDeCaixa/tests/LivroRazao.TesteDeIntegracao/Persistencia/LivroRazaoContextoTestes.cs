using FluentAssertions;
using LivroRazao.Dominio.Caixa;
using LivroRazao.Infraestrutura.Excecoes;
using LivroRazao.Infraestrutura.Persistencia;
using LivroRazao.Infraestrutura.Persistencia.Entidades.Outbox;
using LivroRazao.Infraestrutura.Repositorio;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace LivroRazao.TesteDeIntegracao.Persistencia;

public sealed class LivroRazaoContextoTestes(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task DeveAplicarMigracoes()
    {
        await using var contexto = CriarContexto();

        await contexto.Database.MigrateAsync();
        await LimparDadosAsync(contexto);

        var migracoesPendentes = await contexto.Database.GetPendingMigrationsAsync();

        migracoesPendentes.Should().BeEmpty();
    }

    [Fact]
    public async Task DevePersistirLancamentoComTipoCreditoComoC()
    {
        await ValidarPersistenciaDoTipoAsync(
            TipoDeLancamento.Credito,
            "C"
            );
    }

    [Fact]
    public async Task DevePersistirLancamentoComTipoDebitoComoD()
    {
        await ValidarPersistenciaDoTipoAsync(
            TipoDeLancamento.Debito,
            "D"
            );
    }

    [Fact]
    public async Task DeveImpedirCorrelationIdDuplicado()
    {
        await using var contexto = CriarContexto();
        await contexto.Database.MigrateAsync();
        await LimparDadosAsync(contexto);

        var correlationId = Guid.NewGuid();

        contexto.Lancamentos.Add(CriarLancamento(correlationId: correlationId));
        await contexto.SaveChangesAsync();

        contexto.ChangeTracker.Clear();
        contexto.Lancamentos.Add(CriarLancamento(correlationId: correlationId));

        var acao = () => contexto.SaveChangesAsync();

        await acao.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task DevePersistirLancamentoEOutboxNaMesmaTransacao()
    {
        await using var contexto = CriarContexto();
        await contexto.Database.MigrateAsync();
        await LimparDadosAsync(contexto);

        var unidadeDeTrabalho = new UnidadeDeTrabalho(contexto);
        var lancamento = CriarLancamento();
        var outbox = CriarOutbox(lancamento.CorrelationId);

        await unidadeDeTrabalho.ExecutarEmTransacaoAsync(
            async token =>
            {
                contexto.Lancamentos.Add(lancamento);
                contexto.LancamentosOutbox.Add(outbox);

                await unidadeDeTrabalho.SalvarAlteracoesAsync(token);
            },
            CancellationToken.None
            );

        contexto.ChangeTracker.Clear();

        (await contexto.Lancamentos.AnyAsync(x => x.CorrelationId == lancamento.CorrelationId))
            .Should().BeTrue();

        (await contexto.LancamentosOutbox.AnyAsync(x => x.CorrelationId == lancamento.CorrelationId))
            .Should().BeTrue();
    }

    [Fact]
    public async Task DeveExecutarRollbackQuandoOutboxFalhar()
    {
        await using var contexto = CriarContexto();
        await contexto.Database.MigrateAsync();
        await LimparDadosAsync(contexto);

        var correlationId = Guid.NewGuid();
        contexto.LancamentosOutbox.Add(CriarOutbox(correlationId));
        await contexto.SaveChangesAsync();
        contexto.ChangeTracker.Clear();

        var unidadeDeTrabalho = new UnidadeDeTrabalho(contexto);
        var lancamento = CriarLancamento(correlationId: correlationId);

        var acao = () => unidadeDeTrabalho.ExecutarEmTransacaoAsync(
            async token =>
            {
                contexto.Lancamentos.Add(lancamento);
                contexto.LancamentosOutbox.Add(CriarOutbox(correlationId));

                await unidadeDeTrabalho.SalvarAlteracoesAsync(token);
            },
            CancellationToken.None
            );

        await acao.Should().ThrowAsync<ConflitoDePersistenciaException>();

        await using var verificacao = CriarContexto();

        (await verificacao.Lancamentos.AnyAsync(x => x.CorrelationId == correlationId))
            .Should().BeFalse();
    }

    [Fact]
    public async Task DeveReservarSomenteUmEventoComWorkersConcorrentes()
    {
        await using (var contexto = CriarContexto())
        {
            await contexto.Database.MigrateAsync();
            await LimparDadosAsync(contexto);
            contexto.LancamentosOutbox.Add(CriarOutbox(Guid.NewGuid()));
            await contexto.SaveChangesAsync();
        }

        await using var contextoUm = CriarContexto();
        await using var contextoDois = CriarContexto();

        var repositorioUm = new LancamentoOutboxRepositorio(contextoUm);
        var repositorioDois = new LancamentoOutboxRepositorio(contextoDois);

        var resultados = await Task.WhenAll(
            repositorioUm.ReservarProximoAsync(1, 5, CancellationToken.None),
            repositorioDois.ReservarProximoAsync(1, 5, CancellationToken.None)
            );

        resultados.Count(x => x is not null).Should().Be(1);
    }

    [Fact]
    public async Task DeveRecuperarEventoComReservaExpirada()
    {
        long eventoId;

        await using (var contexto = CriarContexto())
        {
            await contexto.Database.MigrateAsync();
            await LimparDadosAsync(contexto);

            var evento = CriarOutbox(Guid.NewGuid());
            evento.ReservarParaProcessamento();

            contexto.LancamentosOutbox.Add(evento);
            await contexto.SaveChangesAsync();

            eventoId = evento.Id;

            await contexto.Database.ExecuteSqlInterpolatedAsync(
                $"""
                UPDATE "TB_LANCAMENTO_OUTBOX"
                   SET "DATA_ULTIMA_TENTATIVA" = {DateTimeOffset.UtcNow.AddMinutes(-10)}
                 WHERE "ID" = {eventoId}
                """
                );
        }

        await using var novoContexto = CriarContexto();
        var repositorio = new LancamentoOutboxRepositorio(novoContexto);

        var recuperado = await repositorio.ReservarProximoAsync(
            1,
            5,
            CancellationToken.None
            );

        recuperado.Should().NotBeNull();
        recuperado!.Id.Should().Be(eventoId);
        recuperado.Status.Should().Be(StatusDaOutbox.Processando);
    }

    private async Task ValidarPersistenciaDoTipoAsync(
        TipoDeLancamento tipo,
        string valorEsperado
        )
    {
        await using var contexto = CriarContexto();
        await contexto.Database.MigrateAsync();
        await LimparDadosAsync(contexto);

        var lancamento = CriarLancamento(tipo);

        contexto.Lancamentos.Add(lancamento);
        await contexto.SaveChangesAsync();

        await using var conexao = new NpgsqlConnection(fixture.Container.GetConnectionString());
        await conexao.OpenAsync();

        await using var comando = new NpgsqlCommand(
            "SELECT \"TIPO\" FROM \"TB_LANCAMENTO\" WHERE \"ID\" = @id",
            conexao
            );

        comando.Parameters.AddWithValue("id", lancamento.Id);

        var tipoPersistido = (string?)await comando.ExecuteScalarAsync();

        tipoPersistido.Should().Be(valorEsperado);
    }

    private static async Task LimparDadosAsync(LivroRazaoContexto contexto)
    {
        await contexto.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE \"TB_LANCAMENTO_OUTBOX\", \"TB_LANCAMENTO\" RESTART IDENTITY CASCADE"
            );
    }

    private LivroRazaoContexto CriarContexto()
    {
        var opcoes = new DbContextOptionsBuilder<LivroRazaoContexto>()
            .UseNpgsql(fixture.Container.GetConnectionString())
            .Options;

        return new LivroRazaoContexto(opcoes);
    }

    private static Lancamento CriarLancamento(
        TipoDeLancamento tipo = TipoDeLancamento.Credito,
        Guid? correlationId = null
        )
    {
        return new Lancamento(
            correlationId ?? Guid.NewGuid(),
            tipo,
            DateTimeOffset.UtcNow,
            100,
            "Integração"
            );
    }

    private static LancamentoOutbox CriarOutbox(Guid correlationId)
    {
        return new LancamentoOutbox(
            correlationId,
            "EventoDeLancamentoCriado",
            "{}",
            DateTimeOffset.UtcNow
            );
    }
}
