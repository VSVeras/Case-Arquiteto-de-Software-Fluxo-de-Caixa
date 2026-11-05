using LivroRazao.Aplicacao.Abstracao;
using Mensageria.RabbitMQ.Topologia;
using LivroRazao.Infraestrutura.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace LivroRazao.TesteDeUnidade.Infraestrutura;

public sealed class PublicadorOutboxWorkerTestes
{
    [Fact]
    public async Task CancelamentoDeveEncerrarSemRegistrarErro()
    {
        var processador = new Mock<IProcessadorOutbox>();
        var inicializadorRabbitMq = new Mock<IInicializadorRabbitMq>();
        var registroDeEvento = new Mock<IRegistroDeEvento>();

        using var cancelamento = new CancellationTokenSource();

        processador
            .Setup(x => x.ProcessarLoteAsync(
                It.IsAny<CancellationToken>()
                ))
            .Callback(cancelamento.Cancel)
            .ThrowsAsync(
                new OperationCanceledException(cancelamento.Token)
                );

        var worker = CriarWorker(
            processador.Object,
            inicializadorRabbitMq.Object,
            registroDeEvento.Object
            );

        await worker.ExecutarAsync(cancelamento.Token);

        registroDeEvento.Verify(
            x => x.Erro(
                It.IsAny<Exception>(),
                It.IsAny<string>(),
                It.IsAny<object?[]>()
                ),
            Times.Never
            );

        inicializadorRabbitMq.Verify(
            x => x.InicializarAsync(It.IsAny<CancellationToken>()),
            Times.Once
            );
    }

    [Fact]
    public async Task FalhaInesperadaDeveRegistrarErroEContinuar()
    {
        var processador = new Mock<IProcessadorOutbox>();
        var inicializadorRabbitMq = new Mock<IInicializadorRabbitMq>();
        var registroDeEvento = new Mock<IRegistroDeEvento>();

        using var cancelamento = new CancellationTokenSource();

        processador
            .SetupSequence(x => x.ProcessarLoteAsync(
                It.IsAny<CancellationToken>()
                ))
            .ThrowsAsync(
                new InvalidOperationException("Falha inesperada.")
                )
            .Returns(() =>
            {
                cancelamento.Cancel();

                return Task.FromException<bool>(
                    new OperationCanceledException(
                        cancelamento.Token
                        )
                    );
            });

        var worker = CriarWorker(
            processador.Object,
            inicializadorRabbitMq.Object,
            registroDeEvento.Object,
            intervaloEmSegundos: 0
            );

        await worker.ExecutarAsync(cancelamento.Token);

        registroDeEvento.Verify(
            x => x.Erro(
                It.Is<InvalidOperationException>(
                    excecao =>
                        excecao.Message == "Falha inesperada."
                    ),
                "Erro não tratado durante o processamento da Outbox.",
                It.IsAny<object?[]>()
                ),
            Times.Once
            );

        processador.Verify(
            x => x.ProcessarLoteAsync(
                It.IsAny<CancellationToken>()
                ),
            Times.Exactly(2)
            );

        inicializadorRabbitMq.Verify(
            x => x.InicializarAsync(It.IsAny<CancellationToken>()),
            Times.Once
            );
    }


    [Fact]
    public async Task FalhaNaInicializacaoDoRabbitMqDeveRegistrarAvisoETentarNovamente()
    {
        var processador = new Mock<IProcessadorOutbox>();
        var inicializadorRabbitMq = new Mock<IInicializadorRabbitMq>();
        var registroDeEvento = new Mock<IRegistroDeEvento>();
        using var cancelamento = new CancellationTokenSource();

        var tentativasDeInicializacao = 0;

        inicializadorRabbitMq
            .Setup(x => x.InicializarAsync(It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                tentativasDeInicializacao++;

                if (tentativasDeInicializacao == 1)
                {
                    throw new InvalidOperationException("RabbitMQ indisponível.");
                }
            })
            .Returns(Task.CompletedTask);

        processador
            .Setup(x => x.ProcessarLoteAsync(It.IsAny<CancellationToken>()))
            .Callback(cancelamento.Cancel)
            .ThrowsAsync(new OperationCanceledException(cancelamento.Token));

        var worker = CriarWorker(
            processador.Object,
            inicializadorRabbitMq.Object,
            registroDeEvento.Object,
            intervaloEmSegundos: 0);

        await worker.ExecutarAsync(cancelamento.Token);

        inicializadorRabbitMq.Verify(x => x.InicializarAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        registroDeEvento.Verify(
            x => x.Aviso(
                It.Is<InvalidOperationException>(e => e.Message == "RabbitMQ indisponível."),
                "RabbitMQ indisponível. Nova tentativa em {IntervaloEmSegundos} segundos.",
                It.Is<object?[]>(a => a.Length == 1 && Equals(a[0], 0))),
            Times.Once);
    }

    [Fact]
    public async Task EventoEncontradoDeveProcessarNovamenteSemAguardar()
    {
        var processador = new Mock<IProcessadorOutbox>();
        var inicializadorRabbitMq = new Mock<IInicializadorRabbitMq>();
        var registroDeEvento = new Mock<IRegistroDeEvento>();
        using var cancelamento = new CancellationTokenSource();

        processador
            .SetupSequence(x => x.ProcessarLoteAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .Returns(() =>
            {
                cancelamento.Cancel();
                return Task.FromException<bool>(
                    new OperationCanceledException(cancelamento.Token));
            });

        var worker = CriarWorker(
            processador.Object,
            inicializadorRabbitMq.Object,
            registroDeEvento.Object,
            intervaloEmSegundos: 30);

        await worker.ExecutarAsync(cancelamento.Token);

        processador.Verify(
            x => x.ProcessarLoteAsync(It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        registroDeEvento.Verify(
            x => x.Erro(
                It.IsAny<Exception>(),
                It.IsAny<string>(),
                It.IsAny<object?[]>()),
            Times.Never);
    }

    [Fact]
    public async Task SemEventoDeveEncerrarQuandoCanceladoDuranteEspera()
    {
        var processador = new Mock<IProcessadorOutbox>();
        var inicializadorRabbitMq = new Mock<IInicializadorRabbitMq>();
        var registroDeEvento = new Mock<IRegistroDeEvento>();
        using var cancelamento = new CancellationTokenSource();

        processador
            .Setup(x => x.ProcessarLoteAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var worker = CriarWorker(
            processador.Object,
            inicializadorRabbitMq.Object,
            registroDeEvento.Object,
            intervaloEmSegundos: 30);

        var execucao = worker.ExecutarAsync(cancelamento.Token);

        await EsperarProcessamentoAsync(processador);
        cancelamento.Cancel();
        await execucao;

        processador.Verify(
            x => x.ProcessarLoteAsync(It.IsAny<CancellationToken>()),
            Times.Once);
        registroDeEvento.Verify(
            x => x.Informacao("Publicador Outbox finalizado.", It.IsAny<object?[]>()),
            Times.Once);
    }

    [Fact]
    public async Task CancelamentoDuranteFalhaDeInicializacaoDeveEncerrarSemProcessarOutbox()
    {
        var processador = new Mock<IProcessadorOutbox>();
        var inicializadorRabbitMq = new Mock<IInicializadorRabbitMq>();
        var registroDeEvento = new Mock<IRegistroDeEvento>();
        using var cancelamento = new CancellationTokenSource();

        inicializadorRabbitMq
            .Setup(x => x.InicializarAsync(It.IsAny<CancellationToken>()))
            .Callback(cancelamento.Cancel)
            .ThrowsAsync(new OperationCanceledException(cancelamento.Token));

        var worker = CriarWorker(
            processador.Object,
            inicializadorRabbitMq.Object,
            registroDeEvento.Object);

        await worker.ExecutarAsync(cancelamento.Token);

        processador.Verify(
            x => x.ProcessarLoteAsync(It.IsAny<CancellationToken>()),
            Times.Never);
        registroDeEvento.Verify(
            x => x.Aviso(
                It.IsAny<Exception>(),
                It.IsAny<string>(),
                It.IsAny<object?[]>()),
            Times.Never);
    }

    private static async Task EsperarProcessamentoAsync(
        Mock<IProcessadorOutbox> processador)
    {
        for (var tentativa = 0; tentativa < 100; tentativa++)
        {
            if (processador.Invocations.Count > 0)
            {
                return;
            }

            await Task.Delay(10);
        }

        throw new TimeoutException("O processamento da Outbox não foi iniciado no tempo esperado.");
    }

    private static WorkerExposto CriarWorker(
        IProcessadorOutbox processador,
        IInicializadorRabbitMq inicializadorRabbitMq,
        IRegistroDeEvento registroDeEvento,
        int intervaloEmSegundos = 1)
    {
        var servicos = new ServiceCollection();

        servicos.AddScoped(_ => processador);

        var provedor = servicos.BuildServiceProvider();

        return new WorkerExposto(
            provedor.GetRequiredService<IServiceScopeFactory>(),
            inicializadorRabbitMq,
            Options.Create(
                new PublicadorOutboxConfiguracao
                {
                    IntervaloEmSegundos = intervaloEmSegundos,
                    IntervaloDeAlertaDeTentativas = 3,
                    TempoLimiteDeProcessamentoEmMinutos = 5,
                    QuantidadePorLote = 100
                }
                ),
            registroDeEvento
            );
    }

    private sealed class WorkerExposto(
        IServiceScopeFactory fabricaDeEscopo,
        IInicializadorRabbitMq inicializadorRabbitMq,
        IOptions<PublicadorOutboxConfiguracao> opcoes,
        IRegistroDeEvento registroDeEvento)
        : PublicadorOutboxWorker(
            fabricaDeEscopo,
            inicializadorRabbitMq,
            opcoes,
            registroDeEvento)
    {
        public Task ExecutarAsync(
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(cancellationToken);
        }
    }
}
