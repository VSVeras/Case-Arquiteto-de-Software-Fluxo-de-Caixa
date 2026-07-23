using ConsolidadoDiario.Infraestrutura.Dao;
using ConsolidadoDiario.Infraestrutura.Persistencia.Entidade;
using Dapper;
using FluentAssertions;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace ConsolidadoDiario.TesteDeIntegracao.Infraestrutura.Dao;

public sealed class ConsolidadoDiarioDaoTestes : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgreSql = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("relatorio_fluxo_de_caixa_testes")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private NpgsqlDataSource _dataSource = null!;
    private ConsolidadoDiarioDao _dao = null!;

    public async Task InitializeAsync()
    {
        await _postgreSql.StartAsync();

        _dataSource = NpgsqlDataSource.Create(_postgreSql.GetConnectionString());
        _dao = new ConsolidadoDiarioDao(_dataSource);

        var caminhoDaMigracao = Path.Combine(
            AppContext.BaseDirectory,
            "Infraestrutura",
            "Migracao",
            "001_criar_estrutura_inicial.sql");

        var sqlDaMigracao = await File.ReadAllTextAsync(caminhoDaMigracao);

        await using var conexao = await _dataSource.OpenConnectionAsync();
        await conexao.ExecuteAsync(sqlDaMigracao);
    }

    public async Task DisposeAsync()
    {
        await _dataSource.DisposeAsync();
        await _postgreSql.DisposeAsync();
    }

    [Fact]
    public async Task DeveConsolidarCreditoEDebitoNaMesmaData()
    {
        await LimparTabelasAsync();

        var dataReferencia = new DateOnly(2026, 7, 22);
        var credito = CriarHistorico(TipoDeLancamento.Credito, 300m, dataReferencia);
        var debito = CriarHistorico(TipoDeLancamento.Debito, 50m, dataReferencia);

        var creditoProcessado = await _dao.ProcessarLancamentoAsync(
            credito,
            dataReferencia,
            CancellationToken.None);

        var debitoProcessado = await _dao.ProcessarLancamentoAsync(
            debito,
            dataReferencia,
            CancellationToken.None);

        var saldo = await _dao.ObterPorDataAsync(dataReferencia, CancellationToken.None);

        creditoProcessado.Should().BeTrue();
        debitoProcessado.Should().BeTrue();
        saldo.Should().NotBeNull();
        saldo!.DataReferencia.Should().Be(dataReferencia.ToDateTime(TimeOnly.MinValue));
        saldo.TotalCreditos.Should().Be(300m);
        saldo.TotalDebitos.Should().Be(50m);
        saldo.SaldoDiarioConsolidado.Should().Be(250m);
        saldo.DataAtualizacao.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public async Task NaoDeveAlterarSaldoAoReprocessarMesmoCorrelationId()
    {
        await LimparTabelasAsync();

        var dataReferencia = new DateOnly(2026, 7, 22);
        var historico = CriarHistorico(TipoDeLancamento.Credito, 100m, dataReferencia);

        var primeiroProcessamento = await _dao.ProcessarLancamentoAsync(
            historico,
            dataReferencia,
            CancellationToken.None);

        var segundoProcessamento = await _dao.ProcessarLancamentoAsync(
            historico,
            dataReferencia,
            CancellationToken.None);

        var saldo = await _dao.ObterPorDataAsync(dataReferencia, CancellationToken.None);
        var quantidadeNoHistorico = await ObterQuantidadeNoHistoricoAsync(historico.CorrelationId);

        primeiroProcessamento.Should().BeTrue();
        segundoProcessamento.Should().BeFalse();
        quantidadeNoHistorico.Should().Be(1);
        saldo.Should().NotBeNull();
        saldo!.TotalCreditos.Should().Be(100m);
        saldo.TotalDebitos.Should().Be(0m);
        saldo.SaldoDiarioConsolidado.Should().Be(100m);
    }

    [Fact]
    public async Task DeveRetornarNuloQuandoNaoExistirConsolidadoParaData()
    {
        await LimparTabelasAsync();

        var saldo = await _dao.ObterPorDataAsync(
            new DateOnly(2026, 7, 21),
            CancellationToken.None);

        saldo.Should().BeNull();
    }

    private async Task LimparTabelasAsync()
    {
        const string sql = """
            TRUNCATE TABLE
                "TB_HISTORICO_LANCAMENTO",
                "TB_SALDO_CONSOLIDADO_DIARIO"
            RESTART IDENTITY;
            """;

        await using var conexao = await _dataSource.OpenConnectionAsync();
        await conexao.ExecuteAsync(sql);
    }

    private async Task<int> ObterQuantidadeNoHistoricoAsync(Guid correlationId)
    {
        const string sql = """
            SELECT COUNT(*)
            FROM "TB_HISTORICO_LANCAMENTO"
            WHERE "CORRELATION_ID" = @CorrelationId;
            """;

        await using var conexao = await _dataSource.OpenConnectionAsync();

        return await conexao.QuerySingleAsync<int>(sql, new { CorrelationId = correlationId });
    }

    private static HistoricoLancamento CriarHistorico(
        TipoDeLancamento tipo,
        decimal valor,
        DateOnly dataReferencia)
    {
        return new HistoricoLancamento(
            Guid.NewGuid(),
            tipo,
            new DateTimeOffset(
                dataReferencia.Year,
                dataReferencia.Month,
                dataReferencia.Day,
                14,
                30,
                0,
                TimeSpan.Zero),
            valor,
            "Lançamento de teste",
            DateTimeOffset.UtcNow);
    }
}
