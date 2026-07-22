using LivroRazao.Aplicacao.Abstracao;
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
        var registroDeEvento = new Mock<IRegistroDeEvento>();
        using var cancelamento = new CancellationTokenSource();

        processador
            .Setup(x => x.ProcessarProximoAsync(It.IsAny<CancellationToken>()))
            .Callback(cancelamento.Cancel)
            .ThrowsAsync(new OperationCanceledException(cancelamento.Token));

        var worker = CriarWorker(
            processador.Object,
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
    }

    [Fact]
    public async Task FalhaInesperadaDeveRegistrarErroEContinuar()
    {
        var processador = new Mock<IProcessadorOutbox>();
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
            registroDeEvento.Object,
            intervaloEmSegundos: 0
            );

        await worker.ExecutarAsync(cancelamento.Token);

        registroDeEvento.Verify(
            x => x.Erro(
                It.Is<InvalidOperationException>(excecao => excecao.Message == "Falha inesperada."),
                "Erro não tratado durante o processamento da Outbox.",
                It.IsAny<object?[]>()
                ),
            Times.Once
            );

        processador.Verify(
            x => x.ProcessarProximoAsync(It.IsAny<CancellationToken>()),
            Times.Exactly(2)
            );
    }

    private static WorkerExposto CriarWorker(
        IProcessadorOutbox processador,
        IRegistroDeEvento registroDeEvento,
        int intervaloEmSegundos = 1
        )
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => processador);

        var provedor = services.BuildServiceProvider();

        return new WorkerExposto(
            provedor.GetRequiredService<IServiceScopeFactory>(),
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
        IOptions<PublicadorOutboxConfiguracao> opcoes,
        IRegistroDeEvento registroDeEvento)
        : PublicadorOutboxWorker(
            fabricaDeEscopo,
            opcoes,
            registroDeEvento)
    {
        public Task ExecutarAsync(CancellationToken cancellationToken)
        {
            return ExecuteAsync(cancellationToken);
        }
    }
}
