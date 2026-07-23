using LivroRazao.Aplicacao.Abstracao;
using LivroRazao.Infraestrutura.Mensageria;
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
            .Setup(x => x.ProcessarProximoAsync(It.IsAny<CancellationToken>()))
            .Callback(cancelamento.Cancel)
            .ThrowsAsync(new OperationCanceledException(cancelamento.Token));

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
            x => x.Inicializar(),
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
            .SetupSequence(x => x.ProcessarProximoAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Falha inesperada."))
            .Returns(() =>
            {
                cancelamento.Cancel();

                return Task.FromException<bool>(
                    new OperationCanceledException(cancelamento.Token)
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
                    excecao => excecao.Message == "Falha inesperada."
                ),
                "Erro não tratado durante o processamento da Outbox.",
                It.IsAny<object?[]>()
            ),
            Times.Once
        );

        processador.Verify(
            x => x.ProcessarProximoAsync(It.IsAny<CancellationToken>()),
            Times.Exactly(2)
        );

        inicializadorRabbitMq.Verify(
            x => x.Inicializar(),
            Times.Once
        );
    }

    private static WorkerExposto CriarWorker(
        IProcessadorOutbox processador,
        IInicializadorRabbitMq inicializadorRabbitMq,
        IRegistroDeEvento registroDeEvento,
        int intervaloEmSegundos = 1
    )
    {
        var services = new ServiceCollection();

        services.AddScoped(_ => processador);

        var provedor = services.BuildServiceProvider();

        return new WorkerExposto(
            provedor.GetRequiredService<IServiceScopeFactory>(),
            inicializadorRabbitMq,
            Options.Create(
                new PublicadorOutboxConfiguracao
                {
                    IntervaloEmSegundos = intervaloEmSegundos,
                    LimiteDeTentativas = 3,
                    TempoLimiteDeProcessamentoEmMinutos = 5
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
        public Task ExecutarAsync(CancellationToken cancellationToken)
        {
            return ExecuteAsync(cancellationToken);
        }
    }
}
