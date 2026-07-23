using ConsolidadoDiario.Aplicacao.Abstracao;
using ConsolidadoDiario.Aplicacao.Servico;
using ConsolidadoDiario.Infraestrutura.Persistencia.Entidade;
using FluentAssertions;
using Moq;
using Xunit;

namespace ConsolidadoDiario.TesteDeUnidade.Aplicacao.Servico;

public sealed class ConsultaConsolidadoDiarioServicoTestes
{
    private readonly Mock<IConsolidadoDiarioDao> _consolidadoDiarioDao = new();

    [Fact]
    public async Task DeveRetornarConsolidadoQuandoExistirSaldoParaData()
    {
        var dataReferencia = new DateOnly(2026, 7, 22);
        var dataAtualizacao = new DateTime(2026, 7, 22, 20, 17, 38, DateTimeKind.Utc);
        var saldoConsolidado = new SaldoConsolidado(
            dataReferencia.ToDateTime(TimeOnly.MinValue),
            300m,
            50m,
            250m,
            dataAtualizacao);

        _consolidadoDiarioDao
            .Setup(dao => dao.ObterPorDataAsync(dataReferencia, It.IsAny<CancellationToken>()))
            .ReturnsAsync(saldoConsolidado);

        var servico = new ConsultaConsolidadoDiarioServico(_consolidadoDiarioDao.Object);

        var resposta = await servico.ObterPorDataAsync(dataReferencia, CancellationToken.None);

        resposta.Should().NotBeNull();
        resposta!.DataReferencia.Should().Be(dataReferencia);
        resposta.TotalCreditos.Should().Be(300m);
        resposta.TotalDebitos.Should().Be(50m);
        resposta.SaldoDiarioConsolidado.Should().Be(250m);
        resposta.DataAtualizacao.Should().Be(new DateTimeOffset(dataAtualizacao, TimeSpan.Zero));

        _consolidadoDiarioDao.Verify(
            dao => dao.ObterPorDataAsync(dataReferencia, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeveRetornarNuloQuandoNaoExistirSaldoParaData()
    {
        var dataReferencia = new DateOnly(2026, 7, 21);

        _consolidadoDiarioDao
            .Setup(dao => dao.ObterPorDataAsync(dataReferencia, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SaldoConsolidado?)null);

        var servico = new ConsultaConsolidadoDiarioServico(_consolidadoDiarioDao.Object);

        var resposta = await servico.ObterPorDataAsync(dataReferencia, CancellationToken.None);

        resposta.Should().BeNull();
    }

    [Fact]
    public async Task DeveRejeitarDataDeReferenciaNaoInformada()
    {
        var servico = new ConsultaConsolidadoDiarioServico(_consolidadoDiarioDao.Object);

        var acao = () => servico.ObterPorDataAsync(default, CancellationToken.None);

        await acao.Should()
            .ThrowAsync<ArgumentException>()
            .WithParameterName("dataReferencia");

        _consolidadoDiarioDao.VerifyNoOtherCalls();
    }
}
