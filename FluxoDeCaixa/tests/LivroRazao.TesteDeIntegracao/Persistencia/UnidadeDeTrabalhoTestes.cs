using FluentAssertions;
using LivroRazao.Dominio.Caixa;
using LivroRazao.Infraestrutura.Persistencia;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LivroRazao.TesteDeIntegracao.Persistencia;

public sealed class UnidadeDeTrabalhoTestes(
    PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task ExcecaoGenericaDeveExecutarRollbackEPreservarExcecaoOriginal()
    {
        await using var contexto = CriarContexto();
        await PrepararBancoAsync(contexto);
        var unidadeDeTrabalho = new UnidadeDeTrabalho(contexto);
        var lancamento = CriarLancamento();

        Func<Task> acao = () => unidadeDeTrabalho.ExecutarEmTransacaoAsync(
            async token =>
            {
                contexto.Lancamentos.Add(lancamento);
                await unidadeDeTrabalho.SalvarAlteracoesAsync(token);
                throw new InvalidOperationException("Falha controlada.");
            },
            CancellationToken.None);

        await acao.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Falha controlada.");

        await using var verificacao = CriarContexto();
        var existe = await verificacao.Lancamentos
            .AnyAsync(x => x.CorrelationId == lancamento.CorrelationId);

        existe.Should().BeFalse();
    }

    [Fact]
    public async Task LimparRastreamentoDeveRemoverEntidadesDoChangeTracker()
    {
        await using var contexto = CriarContexto();
        await PrepararBancoAsync(contexto);
        var unidadeDeTrabalho = new UnidadeDeTrabalho(contexto);
        contexto.Lancamentos.Add(CriarLancamento());

        contexto.ChangeTracker.Entries().Should().NotBeEmpty();

        unidadeDeTrabalho.LimparRastreamento();

        contexto.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    public async Task SalvarAlteracoesAsyncDevePersistirAlteracoes()
    {
        await using var contexto = CriarContexto();
        await PrepararBancoAsync(contexto);
        var unidadeDeTrabalho = new UnidadeDeTrabalho(contexto);
        var lancamento = CriarLancamento();
        contexto.Lancamentos.Add(lancamento);

        await unidadeDeTrabalho.SalvarAlteracoesAsync(CancellationToken.None);

        lancamento.Id.Should().BeGreaterThan(0);
    }

    private static Lancamento CriarLancamento()
    {
        return new Lancamento(
            Guid.NewGuid(),
            TipoDeLancamento.Credito,
            DateTimeOffset.UtcNow,
            100,
            "Teste de unidade de trabalho");
    }

    private static async Task PrepararBancoAsync(LivroRazaoContexto contexto)
    {
        await contexto.Database.MigrateAsync();
        await contexto.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE \"TB_LANCAMENTO_OUTBOX\", \"TB_LANCAMENTO\" RESTART IDENTITY CASCADE");
    }

    private LivroRazaoContexto CriarContexto()
    {
        var opcoes = new DbContextOptionsBuilder<LivroRazaoContexto>()
            .UseNpgsql(fixture.Container.GetConnectionString())
            .Options;

        return new LivroRazaoContexto(opcoes);
    }
}
