using System.Reflection;
using System.Text;
using System.Text.Json;
using ConsolidadoDiario.Aplicacao.Abstracao;
using ConsolidadoDiario.Aplicacao.EventosDeIntegracao;
using ConsolidadoDiario.Infraestrutura.Mensageria;
using ConsolidadoDiario.Infraestrutura.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Xunit;

namespace ConsolidadoDiario.TesteDeUnidade.Infraestrutura.Worker;

public sealed class ConsumidorConsolidadoWorkerTestes
{
    [Fact]
    public async Task DeveConfirmarMensagemProcessada()
    {
        var processador = new Mock<IProcessadorEventoLancamento>();

        processador
            .Setup(x => x.ProcessarAsync(
                It.IsAny<EventoDeLancamentoCriado>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var contexto = CriarContexto(processador.Object);
        var argumentos = CriarArgumentos(CriarEvento());

        await InvocarProcessamentoAsync(
            contexto.Worker,
            argumentos,
            CancellationToken.None);

        contexto.Canal.Verify(
            x => x.BasicAck(argumentos.DeliveryTag, false),
            Times.Once);

        contexto.Canal.Verify(
            x => x.BasicReject(
                It.IsAny<ulong>(),
                It.IsAny<bool>()),
            Times.Never);

        contexto.Canal.Verify(
            x => x.BasicNack(
                It.IsAny<ulong>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()),
            Times.Never);
    }

    [Fact]
    public async Task DeveConfirmarMensagemJaProcessada()
    {
        var processador = new Mock<IProcessadorEventoLancamento>();

        processador
            .Setup(x => x.ProcessarAsync(
                It.IsAny<EventoDeLancamentoCriado>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var contexto = CriarContexto(processador.Object);
        var argumentos = CriarArgumentos(CriarEvento());

        await InvocarProcessamentoAsync(
            contexto.Worker,
            argumentos,
            CancellationToken.None);

        contexto.Canal.Verify(
            x => x.BasicAck(argumentos.DeliveryTag, false),
            Times.Once);
    }

    [Fact]
    public async Task DeveDescartarMensagemComJsonInvalido()
    {
        var contexto =
            CriarContexto(Mock.Of<IProcessadorEventoLancamento>());

        var argumentos =
            CriarArgumentos(
                Encoding.UTF8.GetBytes("{ json inválido"));

        await InvocarProcessamentoAsync(
            contexto.Worker,
            argumentos,
            CancellationToken.None);

        contexto.Canal.Verify(
            x => x.BasicReject(argumentos.DeliveryTag, false),
            Times.Once);
    }

    [Fact]
    public async Task DeveDescartarEventoComCorrelationIdVazio()
    {
        var contexto =
            CriarContexto(Mock.Of<IProcessadorEventoLancamento>());

        var argumentos =
            CriarArgumentos(
                CriarEvento(correlationId: Guid.Empty));

        await InvocarProcessamentoAsync(
            contexto.Worker,
            argumentos,
            CancellationToken.None);

        contexto.Canal.Verify(
            x => x.BasicReject(argumentos.DeliveryTag, false),
            Times.Once);
    }

    [Fact]
    public async Task DeveReenfileirarMensagemQuandoProcessamentoFalhar()
    {
        var processador = new Mock<IProcessadorEventoLancamento>();

        processador
            .Setup(x => x.ProcessarAsync(
                It.IsAny<EventoDeLancamentoCriado>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Falha."));

        var contexto = CriarContexto(processador.Object);
        var argumentos = CriarArgumentos(CriarEvento());

        await InvocarProcessamentoAsync(
            contexto.Worker,
            argumentos,
            CancellationToken.None);

        contexto.Canal.Verify(
            x => x.BasicNack(
                10,
                false,
                false),
            Times.Once);
    }

    [Fact]
    public async Task DeveReenfileirarMensagemQuandoProcessamentoForCancelado()
    {
        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        var processador = new Mock<IProcessadorEventoLancamento>();

        processador
            .Setup(x => x.ProcessarAsync(
                It.IsAny<EventoDeLancamentoCriado>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new OperationCanceledException(
                    cancellationTokenSource.Token));

        var contexto = CriarContexto(processador.Object);
        var argumentos = CriarArgumentos(CriarEvento());

        await InvocarProcessamentoAsync(
            contexto.Worker,
            argumentos,
            cancellationTokenSource.Token);

        contexto.Canal.Verify(
            x => x.BasicNack(
                argumentos.DeliveryTag,
                false,
                true),
            Times.Once);
    }

    [Fact]
    public async Task NaoDeveProcessarQuandoCanalEstiverFechado()
    {
        var processador =
            new Mock<IProcessadorEventoLancamento>();

        var contexto =
            CriarContexto(
                processador.Object,
                canalAberto: false);

        var argumentos =
            CriarArgumentos(CriarEvento());

        await InvocarProcessamentoAsync(
            contexto.Worker,
            argumentos,
            CancellationToken.None);

        processador.Verify(
            x => x.ProcessarAsync(
                It.IsAny<EventoDeLancamentoCriado>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void DeveFecharEDescartarCanalAoDescartarWorker()
    {
        var contexto =
            CriarContexto(
                Mock.Of<IProcessadorEventoLancamento>());

        contexto.Worker.Dispose();

        contexto.Canal.Verify(
            x => x.Close(),
            Times.Once);

        contexto.Canal.Verify(
            x => x.Dispose(),
            Times.Once);
    }

    [Fact]
    public void DeveDescartarCanalFechadoSemTentarFecharNovamente()
    {
        var contexto =
            CriarContexto(
                Mock.Of<IProcessadorEventoLancamento>(),
                canalAberto: false);

        contexto.Worker.Dispose();

        contexto.Canal.Verify(
            x => x.Close(),
            Times.Never);

        contexto.Canal.Verify(
            x => x.Dispose(),
            Times.Once);
    }

    [Theory]
    [InlineData("cabecalho-invalido", null)]
    [InlineData(null, "tipo.invalido")]
    public async Task DeveDescartarMensagemComMetadadosInvalidos(
        string? correlationId,
        string? tipo)
    {
        var evento = CriarEvento();

        var contexto =
            CriarContexto(
                Mock.Of<IProcessadorEventoLancamento>());

        var argumentos =
            CriarArgumentos(
                evento,
                correlationId,
                tipo);

        await InvocarProcessamentoAsync(
            contexto.Worker,
            argumentos,
            CancellationToken.None);

        contexto.Canal.Verify(
            x => x.BasicReject(
                argumentos.DeliveryTag,
                false),
            Times.Once);
    }

    private static ContextoDoWorker CriarContexto(
        IProcessadorEventoLancamento processador,
        bool canalAberto = true)
    {
        var canal = new Mock<IModel>();

        canal
            .SetupGet(x => x.IsOpen)
            .Returns(canalAberto);

        var conexao = new Mock<IConnection>();

        conexao
            .Setup(x => x.CreateModel())
            .Returns(canal.Object);

        var rabbitMqConexao =
            new Mock<IRabbitMqConexao>();

        rabbitMqConexao
            .Setup(x => x.ObterConexao())
            .Returns(conexao.Object);

        var inicializadorRabbitMq =
            new Mock<IInicializadorRabbitMq>();

        var servicos =
            new ServiceCollection();

        servicos.AddSingleton(processador);

        var provedor =
            servicos.BuildServiceProvider();

        var worker =
            new ConsumidorConsolidadoWorker(
                rabbitMqConexao.Object,
                inicializadorRabbitMq.Object,
                provedor.GetRequiredService<IServiceScopeFactory>(),
                Options.Create(new RabbitMqConfiguracao()),
                Mock.Of<IRegistroDeEvento>());

        typeof(ConsumidorConsolidadoWorker)
            .GetField(
                "_canal",
                BindingFlags.Instance |
                BindingFlags.NonPublic)!
            .SetValue(
                worker,
                canal.Object);

        return new ContextoDoWorker(
            worker,
            canal);
    }

    private static BasicDeliverEventArgs CriarArgumentos(
        EventoDeLancamentoCriado evento,
        string? correlationId = null,
        string? tipo = null)
    {
        return CriarArgumentos(
            JsonSerializer.SerializeToUtf8Bytes(
                evento,
                new JsonSerializerOptions(
                    JsonSerializerDefaults.Web)),
            correlationId ??
            evento.CorrelationId.ToString(),
            tipo ??
            evento.TipoEvento);
    }

    private static BasicDeliverEventArgs CriarArgumentos(
        byte[] conteudo,
        string? correlationId = null,
        string? tipo = null)
    {
        var propriedades =
            new Mock<IBasicProperties>();

        propriedades
            .SetupProperty(
                x => x.CorrelationId,
                correlationId);

        propriedades
            .SetupProperty(
                x => x.Type,
                tipo);

        return new BasicDeliverEventArgs
        {
            DeliveryTag = 10,
            BasicProperties = propriedades.Object,
            Body = conteudo
        };
    }

    private static EventoDeLancamentoCriado CriarEvento(
        Guid? correlationId = null)
    {
        return new EventoDeLancamentoCriado(
            correlationId ??
            Guid.NewGuid(),
            "LancamentoCriado",
            new DateTimeOffset(
                2026,
                7,
                22,
                14,
                30,
                0,
                TimeSpan.Zero),
            new DadosDoLancamento(
                1,
                "CREDITO",
                new DateTimeOffset(
                    2026,
                    7,
                    22,
                    14,
                    30,
                    0,
                    TimeSpan.Zero),
                100m,
                "Descrição"));
    }

    private static async Task InvocarProcessamentoAsync(
        ConsumidorConsolidadoWorker worker,
        BasicDeliverEventArgs argumentos,
        CancellationToken cancellationToken)
    {
        var metodo =
            typeof(ConsumidorConsolidadoWorker)
                .GetMethod(
                    "ProcessarMensagemAsync",
                    BindingFlags.Instance |
                    BindingFlags.NonPublic)!;

        var tarefa =
            (Task)metodo.Invoke(
                worker,
                [argumentos, cancellationToken])!;

        await tarefa;
    }

    private sealed record ContextoDoWorker(
        ConsumidorConsolidadoWorker Worker,
        Mock<IModel> Canal);
}
