using FluentAssertions;
using LivroRazao.Dominio.Caixa;
using LivroRazao.Infraestrutura.Persistencia;
using LivroRazao.Infraestrutura.Repositorio;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LivroRazao.TesteDeIntegracao.Persistencia;

public sealed class LancamentoRepositorioTestes(
    PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task AdicionarAsyncDeveAdicionarLancamentoAoContexto()
    {
        await using var contexto = CriarContexto();
        await PrepararBancoAsync(contexto);
        var repositorio = new LancamentoRepositorio(contexto);
        var lancamento = CriarLancamento();

        await repositorio.AdicionarAsync(lancamento, CancellationToken.None);
        await contexto.SaveChangesAsync();

        var persistido = await contexto.Lancamentos
            .AsNoTracking()
            .SingleAsync(x => x.CorrelationId == lancamento.CorrelationId);

        persistido.Id.Should().BeGreaterThan(0);
        persistido.CorrelationId.Should().Be(lancamento.CorrelationId);
    }

    [Fact]
    public async Task ObterPorIdAsyncDeveRetornarLancamentoExistenteSemRastreamento()
    {
        await using var contexto = CriarContexto();
        await PrepararBancoAsync(contexto);
        var lancamento = CriarLancamento();
        contexto.Lancamentos.Add(lancamento);
        await contexto.SaveChangesAsync();
        contexto.ChangeTracker.Clear();
        var repositorio = new LancamentoRepositorio(contexto);

        var resultado = await repositorio.ObterPorIdAsync(
            lancamento.Id,
            CancellationToken.None);

        resultado.Should().NotBeNull();
        resultado!.CorrelationId.Should().Be(lancamento.CorrelationId);
        contexto.ChangeTracker.Entries<Lancamento>().Should().BeEmpty();
    }

    [Fact]
    public async Task ObterPorIdAsyncDeveRetornarNuloQuandoNaoExistir()
    {
        await using var contexto = CriarContexto();
        await PrepararBancoAsync(contexto);
        var repositorio = new LancamentoRepositorio(contexto);

        var resultado = await repositorio.ObterPorIdAsync(
            long.MaxValue,
            CancellationToken.None);

        resultado.Should().BeNull();
    }

    [Fact]
    public async Task ObterPorCorrelationIdAsyncDeveRetornarLancamentoExistenteSemRastreamento()
    {
        await using var contexto = CriarContexto();
        await PrepararBancoAsync(contexto);
        var lancamento = CriarLancamento();
        contexto.Lancamentos.Add(lancamento);
        await contexto.SaveChangesAsync();
        contexto.ChangeTracker.Clear();
        var repositorio = new LancamentoRepositorio(contexto);

        var resultado = await repositorio.ObterPorCorrelationIdAsync(
            lancamento.CorrelationId,
            CancellationToken.None);

        resultado.Should().NotBeNull();
        resultado!.Id.Should().Be(lancamento.Id);
        contexto.ChangeTracker.Entries<Lancamento>().Should().BeEmpty();
    }

    [Fact]
    public async Task ObterPorCorrelationIdAsyncDeveRetornarNuloQuandoNaoExistir()
    {
        await using var contexto = CriarContexto();
        await PrepararBancoAsync(contexto);
        var repositorio = new LancamentoRepositorio(contexto);

        var resultado = await repositorio.ObterPorCorrelationIdAsync(
            Guid.NewGuid(),
            CancellationToken.None);

        resultado.Should().BeNull();
    }

    private static Lancamento CriarLancamento()
    {
        return new Lancamento(
            Guid.NewGuid(),
            TipoDeLancamento.Credito,
            DateTimeOffset.UtcNow,
            100,
            "Teste de repositório");
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
