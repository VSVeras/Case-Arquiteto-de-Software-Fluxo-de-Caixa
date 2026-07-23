using ConsolidadoDiario.Aplicacao.Abstracao;
using ConsolidadoDiario.Aplicacao.EventosDeIntegracao;
using ConsolidadoDiario.Aplicacao.Servico;
using ConsolidadoDiario.Infraestrutura.Persistencia.Entidade;
using FluentAssertions;
using Moq;
using Xunit;

namespace ConsolidadoDiario.TesteDeUnidade.Aplicacao.Servico;

public sealed class ProcessadorEventoLancamentoTestes
{
    private readonly Mock<IConsolidadoDiarioDao> _consolidadoDiarioDao = new();

    [Theory]
    [InlineData("C", TipoDeLancamento.Credito)]
    [InlineData("D", TipoDeLancamento.Debito)]
    public async Task DeveProcessarEventoValido(
        string tipoDoEvento,
        TipoDeLancamento tipoEsperado)
    {
        var correlationId = Guid.NewGuid();
        var dataLancamento = new DateTimeOffset(2026, 7, 22, 14, 30, 0, TimeSpan.Zero);
        var evento = CriarEvento(correlationId, tipoDoEvento, dataLancamento, 125.50m, "Lançamento");
        HistoricoLancamento? historicoRecebido = null;
        DateOnly dataReferenciaRecebida = default;

        _consolidadoDiarioDao
            .Setup(dao => dao.ProcessarLancamentoAsync(
                It.IsAny<HistoricoLancamento>(),
                It.IsAny<DateOnly>(),
                It.IsAny<CancellationToken>()))
            .Callback<HistoricoLancamento, DateOnly, CancellationToken>((historico, dataReferencia, _) =>
            {
                historicoRecebido = historico;
                dataReferenciaRecebida = dataReferencia;
            })
            .ReturnsAsync(true);

        var processador = new ProcessadorEventoLancamento(_consolidadoDiarioDao.Object);

        var foiProcessado = await processador.ProcessarAsync(evento, CancellationToken.None);

        foiProcessado.Should().BeTrue();
        historicoRecebido.Should().NotBeNull();
        historicoRecebido!.CorrelationId.Should().Be(correlationId);
        historicoRecebido.Tipo.Should().Be(tipoEsperado);
        historicoRecebido.DataLancamento.Should().Be(dataLancamento);
        historicoRecebido.Valor.Should().Be(125.50m);
        historicoRecebido.Descricao.Should().Be("Lançamento");
        historicoRecebido.DataProcessamento.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        dataReferenciaRecebida.Should().Be(new DateOnly(2026, 7, 22));
    }

    [Fact]
    public async Task DeveRetornarFalsoQuandoLancamentoJaTiverSidoProcessado()
    {
        var evento = CriarEvento(
            Guid.NewGuid(),
            "C",
            new DateTimeOffset(2026, 7, 22, 14, 30, 0, TimeSpan.Zero),
            100m,
            null);

        _consolidadoDiarioDao
            .Setup(dao => dao.ProcessarLancamentoAsync(
                It.IsAny<HistoricoLancamento>(),
                It.IsAny<DateOnly>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var processador = new ProcessadorEventoLancamento(_consolidadoDiarioDao.Object);

        var foiProcessado = await processador.ProcessarAsync(evento, CancellationToken.None);

        foiProcessado.Should().BeFalse();
    }

    [Fact]
    public async Task DeveRejeitarTipoDeEventoInvalido()
    {
        var evento = new EventoDeLancamentoCriado(
            Guid.NewGuid(),
            "OutroEvento",
            DateTimeOffset.UtcNow,
            new DadosDoLancamento(1, "C", DateTimeOffset.UtcNow, 100m, null));

        var processador = new ProcessadorEventoLancamento(_consolidadoDiarioDao.Object);

        var acao = () => processador.ProcessarAsync(evento, CancellationToken.None);

        await acao.Should()
            .ThrowAsync<ArgumentException>()
            .WithParameterName("evento");

        _consolidadoDiarioDao.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("")]
    [InlineData("X")]
    [InlineData("c")]
    public async Task DeveRejeitarTipoDeLancamentoInvalido(string tipo)
    {
        var evento = CriarEvento(
            Guid.NewGuid(),
            tipo,
            new DateTimeOffset(2026, 7, 22, 14, 30, 0, TimeSpan.Zero),
            100m,
            null);

        var processador = new ProcessadorEventoLancamento(_consolidadoDiarioDao.Object);

        var acao = () => processador.ProcessarAsync(evento, CancellationToken.None);

        await acao.Should()
            .ThrowAsync<ArgumentException>()
            .WithParameterName("evento");

        _consolidadoDiarioDao.VerifyNoOtherCalls();
    }

    private static EventoDeLancamentoCriado CriarEvento(
        Guid correlationId,
        string tipo,
        DateTimeOffset dataLancamento,
        decimal valor,
        string? descricao)
    {
        return new EventoDeLancamentoCriado(
            correlationId,
            nameof(EventoDeLancamentoCriado),
            DateTimeOffset.UtcNow,
            new DadosDoLancamento(
                1,
                tipo,
                dataLancamento,
                valor,
                descricao));
    }
}
