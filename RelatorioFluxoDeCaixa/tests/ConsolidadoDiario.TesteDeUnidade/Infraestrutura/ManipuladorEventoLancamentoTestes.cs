using System.Text.Json;
using ConsolidadoDiario.Aplicacao.Abstracao;
using ConsolidadoDiario.Aplicacao.EventosDeIntegracao;
using ConsolidadoDiario.Infraestrutura.Worker;
using FluentAssertions;
using Mensageria.Assinante;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace ConsolidadoDiario.TesteDeUnidade.Infraestrutura;

public sealed class ManipuladorEventoLancamentoTestes
{
    private static readonly JsonSerializerOptions OpcoesDeSerializacao = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task ProcessarAsync_DeveRetornarSucesso_QuandoEventoForProcessado()
    {
        var evento = CriarEvento();
        var processador = new Mock<IProcessadorEventoLancamento>();
        processador.Setup(x => x.ProcessarAsync(evento, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var registro = new Mock<IRegistroDeEvento>();
        using var provedor = CriarProvedor(processador.Object);
        var sut = new ManipuladorEventoLancamento(provedor.GetRequiredService<IServiceScopeFactory>(), registro.Object);

        var resultado = await sut.ProcessarAsync(CriarMensagem(evento), CancellationToken.None);

        resultado.Tipo.Should().Be(TipoResultadoProcessamento.Sucesso);
        processador.Verify(x => x.ProcessarAsync(evento, It.IsAny<CancellationToken>()), Times.Once);
        registro.Verify(x => x.Informacao(
            "Lançamento consolidado. CorrelationId {CorrelationId}.",
            It.Is<object?[]>(a => a.Length == 1 && Equals(a[0], evento.CorrelationId))), Times.Once);
    }

    [Fact]
    public async Task ProcessarAsync_DeveRetornarSucesso_QuandoEventoJaTiverSidoProcessado()
    {
        var evento = CriarEvento();
        var processador = new Mock<IProcessadorEventoLancamento>();
        processador.Setup(x => x.ProcessarAsync(evento, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var registro = new Mock<IRegistroDeEvento>();
        using var provedor = CriarProvedor(processador.Object);
        var sut = new ManipuladorEventoLancamento(provedor.GetRequiredService<IServiceScopeFactory>(), registro.Object);

        var resultado = await sut.ProcessarAsync(CriarMensagem(evento), CancellationToken.None);

        resultado.Tipo.Should().Be(TipoResultadoProcessamento.Sucesso);
        registro.Verify(x => x.Informacao(
            "Lançamento já processado. CorrelationId {CorrelationId}.",
            It.Is<object?[]>(a => a.Length == 1 && Equals(a[0], evento.CorrelationId))), Times.Once);
    }

    [Fact]
    public async Task ProcessarAsync_DeveAceitarMensagemSemMetadadosOpcionais()
    {
        var evento = CriarEvento();
        var processador = new Mock<IProcessadorEventoLancamento>();
        processador.Setup(x => x.ProcessarAsync(evento, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        using var provedor = CriarProvedor(processador.Object);
        var sut = new ManipuladorEventoLancamento(provedor.GetRequiredService<IServiceScopeFactory>(), Mock.Of<IRegistroDeEvento>());
        var mensagem = new MensagemRecebida(
            null,
            " ",
            JsonSerializer.Serialize(evento, OpcoesDeSerializacao),
            new Dictionary<string, object?>());

        var resultado = await sut.ProcessarAsync(mensagem, CancellationToken.None);

        resultado.Tipo.Should().Be(TipoResultadoProcessamento.Sucesso);
        processador.Verify(x => x.ProcessarAsync(evento, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessarAsync_DeveRetornarErroDefinitivo_QuandoJsonForInvalido()
    {
        var registro = new Mock<IRegistroDeEvento>();
        var processador = new Mock<IProcessadorEventoLancamento>();
        using var provedor = CriarProvedor(processador.Object);
        var sut = new ManipuladorEventoLancamento(provedor.GetRequiredService<IServiceScopeFactory>(), registro.Object);
        var mensagem = new MensagemRecebida(Guid.NewGuid(), "LancamentoCriado", "{ json-invalido", new Dictionary<string, object?>());

        var resultado = await sut.ProcessarAsync(mensagem, CancellationToken.None);

        resultado.Tipo.Should().Be(TipoResultadoProcessamento.ErroDefinitivo);
        resultado.Excecao.Should().BeOfType<JsonException>();
        processador.Verify(x => x.ProcessarAsync(It.IsAny<EventoDeLancamentoCriado>(), It.IsAny<CancellationToken>()), Times.Never);
        registro.Verify(x => x.Aviso(It.IsAny<JsonException>(), It.IsAny<string>(), It.IsAny<object?[]>()), Times.Once);
    }

    [Fact]
    public async Task ProcessarAsync_DeveRetornarErroDefinitivo_QuandoCorrelationIdDoEventoForVazio()
    {
        var evento = CriarEvento(Guid.Empty);
        var registro = new Mock<IRegistroDeEvento>();
        var processador = new Mock<IProcessadorEventoLancamento>();
        using var provedor = CriarProvedor(processador.Object);
        var sut = new ManipuladorEventoLancamento(provedor.GetRequiredService<IServiceScopeFactory>(), registro.Object);

        var resultado = await sut.ProcessarAsync(CriarMensagem(evento, correlationIdCabecalho: null), CancellationToken.None);

        resultado.Tipo.Should().Be(TipoResultadoProcessamento.ErroDefinitivo);
        resultado.Excecao.Should().BeOfType<ArgumentException>();
        processador.Verify(x => x.ProcessarAsync(It.IsAny<EventoDeLancamentoCriado>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessarAsync_DeveRetornarErroDefinitivo_QuandoCorrelationIdDoCabecalhoDivergirDoEvento()
    {
        var evento = CriarEvento();
        var processador = new Mock<IProcessadorEventoLancamento>();
        using var provedor = CriarProvedor(processador.Object);
        var sut = new ManipuladorEventoLancamento(provedor.GetRequiredService<IServiceScopeFactory>(), Mock.Of<IRegistroDeEvento>());

        var resultado = await sut.ProcessarAsync(CriarMensagem(evento, Guid.NewGuid()), CancellationToken.None);

        resultado.Tipo.Should().Be(TipoResultadoProcessamento.ErroDefinitivo);
        resultado.Excecao.Should().BeOfType<ArgumentException>();
        processador.Verify(x => x.ProcessarAsync(It.IsAny<EventoDeLancamentoCriado>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessarAsync_DeveRetornarErroDefinitivo_QuandoTipoDoCabecalhoDivergirDoEvento()
    {
        var evento = CriarEvento();
        var processador = new Mock<IProcessadorEventoLancamento>();
        using var provedor = CriarProvedor(processador.Object);
        var sut = new ManipuladorEventoLancamento(provedor.GetRequiredService<IServiceScopeFactory>(), Mock.Of<IRegistroDeEvento>());

        var resultado = await sut.ProcessarAsync(CriarMensagem(evento, tipo: "OutroEvento"), CancellationToken.None);

        resultado.Tipo.Should().Be(TipoResultadoProcessamento.ErroDefinitivo);
        resultado.Excecao.Should().BeOfType<ArgumentException>();
        processador.Verify(x => x.ProcessarAsync(It.IsAny<EventoDeLancamentoCriado>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessarAsync_DeveRetornarErroTransitorio_QuandoProcessadorFalhar()
    {
        var evento = CriarEvento();
        var excecao = new InvalidOperationException("Falha temporária.");
        var processador = new Mock<IProcessadorEventoLancamento>();
        processador.Setup(x => x.ProcessarAsync(evento, It.IsAny<CancellationToken>())).ThrowsAsync(excecao);
        var registro = new Mock<IRegistroDeEvento>();
        using var provedor = CriarProvedor(processador.Object);
        var sut = new ManipuladorEventoLancamento(provedor.GetRequiredService<IServiceScopeFactory>(), registro.Object);

        var resultado = await sut.ProcessarAsync(CriarMensagem(evento), CancellationToken.None);

        resultado.Tipo.Should().Be(TipoResultadoProcessamento.ErroTransitorio);
        resultado.Excecao.Should().BeSameAs(excecao);
        registro.Verify(x => x.Erro(excecao, It.IsAny<string>(), It.IsAny<object?[]>()), Times.Once);
    }

    [Fact]
    public async Task ProcessarAsync_DevePropagarCancelamento_QuandoTokenForCancelado()
    {
        var evento = CriarEvento();
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();
        var processador = new Mock<IProcessadorEventoLancamento>();
        processador.Setup(x => x.ProcessarAsync(evento, cancellationTokenSource.Token))
            .ThrowsAsync(new OperationCanceledException(cancellationTokenSource.Token));
        using var provedor = CriarProvedor(processador.Object);
        var sut = new ManipuladorEventoLancamento(provedor.GetRequiredService<IServiceScopeFactory>(), Mock.Of<IRegistroDeEvento>());

        var acao = () => sut.ProcessarAsync(CriarMensagem(evento), cancellationTokenSource.Token);

        await acao.Should().ThrowAsync<OperationCanceledException>();
    }

    private static ServiceProvider CriarProvedor(IProcessadorEventoLancamento processador)
    {
        var servicos = new ServiceCollection();
        servicos.AddScoped(_ => processador);
        return servicos.BuildServiceProvider();
    }

    private static EventoDeLancamentoCriado CriarEvento(Guid? correlationId = null) =>
        new(
            correlationId ?? Guid.NewGuid(),
            "LancamentoCriado",
            DateTimeOffset.UtcNow,
            new DadosDoLancamento(123, "CREDITO", DateTimeOffset.UtcNow, 150.75m, "Teste"));

    private static MensagemRecebida CriarMensagem(
        EventoDeLancamentoCriado evento,
        Guid? correlationIdCabecalho = null,
        string? tipo = null) =>
        new(
            correlationIdCabecalho ?? evento.CorrelationId,
            tipo ?? evento.TipoEvento,
            JsonSerializer.Serialize(evento, OpcoesDeSerializacao),
            new Dictionary<string, object?>());
}
