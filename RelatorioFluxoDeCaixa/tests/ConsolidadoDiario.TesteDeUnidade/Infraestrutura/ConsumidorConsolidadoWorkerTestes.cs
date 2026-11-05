using ConsolidadoDiario.Aplicacao.Abstracao;
using ConsolidadoDiario.Infraestrutura.Worker;
using FluentAssertions;
using Mensageria.Assinante;
using Moq;
using Xunit;

namespace ConsolidadoDiario.TesteDeUnidade.Infraestrutura;

public sealed class ConsumidorConsolidadoWorkerTestes
{
    [Fact]
    public async Task Worker_DeveAssinarConsumidor_ERegistrarInicioEFinalizacao()
    {
        var assinante = new Mock<IAssinante>();
        var manipulador = Mock.Of<IManipuladorDeMensagem>();
        var registro = new Mock<IRegistroDeEvento>();
        var consumoIniciado = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        assinante
            .Setup(x => x.AssinarAsync(manipulador, It.IsAny<CancellationToken>()))
            .Returns<IManipuladorDeMensagem, CancellationToken>(async (_, token) =>
            {
                consumoIniciado.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            });
        var sut = new ConsumidorConsolidadoWorker(assinante.Object, manipulador, registro.Object);

        await sut.StartAsync(CancellationToken.None);
        await consumoIniciado.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await sut.StopAsync(CancellationToken.None);

        assinante.Verify(x => x.AssinarAsync(manipulador, It.IsAny<CancellationToken>()), Times.Once);
        registro.Verify(x => x.Informacao("Consumidor do consolidado diário iniciado.", It.IsAny<object?[]>()), Times.Once);
        registro.Verify(x => x.Informacao("Consumidor do consolidado diário finalizado.", It.IsAny<object?[]>()), Times.Once);
    }

    [Fact]
    public async Task Worker_DeveTentarNovamente_AposFalhaInicialDoRabbitMq()
    {
        var assinante = new Mock<IAssinante>();
        var manipulador = Mock.Of<IManipuladorDeMensagem>();
        var registro = new Mock<IRegistroDeEvento>();
        var segundaTentativa = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var chamadas = 0;
        assinante.Setup(x => x.AssinarAsync(manipulador, It.IsAny<CancellationToken>()))
            .Returns<IManipuladorDeMensagem, CancellationToken>(async (_, token) =>
            {
                if (Interlocked.Increment(ref chamadas) == 1)
                    throw new IOException("RabbitMQ temporariamente indisponível");
                segundaTentativa.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            });

        var sut = new ConsumidorConsolidadoWorker(assinante.Object, manipulador, registro.Object);
        await sut.StartAsync(CancellationToken.None);
        try
        {
            await segundaTentativa.Task.WaitAsync(TimeSpan.FromSeconds(5));
            assinante.Verify(x => x.AssinarAsync(manipulador, It.IsAny<CancellationToken>()), Times.Exactly(2));
            registro.Verify(x => x.Aviso(It.IsAny<Exception>(), It.IsAny<string>(), It.IsAny<object?[]>()), Times.Once);
        }
        finally
        {
            await sut.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task Worker_DeveRecuperarDeExcecaoNaoClassificada()
    {
        var assinante = new Mock<IAssinante>();
        var manipulador = Mock.Of<IManipuladorDeMensagem>();
        var registro = new Mock<IRegistroDeEvento>();
        var recuperou = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var chamadas = 0;
        assinante.Setup(x => x.AssinarAsync(manipulador, It.IsAny<CancellationToken>()))
            .Returns<IManipuladorDeMensagem, CancellationToken>(async (_, token) =>
            {
                if (Interlocked.Increment(ref chamadas) == 1)
                    throw new InvalidOperationException("Falha inesperada na assinatura");
                recuperou.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            });

        var sut = new ConsumidorConsolidadoWorker(assinante.Object, manipulador, registro.Object);
        await sut.StartAsync(CancellationToken.None);
        try
        {
            await recuperou.Task.WaitAsync(TimeSpan.FromSeconds(5));
            assinante.Verify(x => x.AssinarAsync(manipulador, It.IsAny<CancellationToken>()), Times.Exactly(2));
        }
        finally
        {
            await sut.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task Worker_DeveEncerrarDuranteBackoff_SemNovaTentativa()
    {
        var assinante = new Mock<IAssinante>();
        var manipulador = Mock.Of<IManipuladorDeMensagem>();
        var registro = new Mock<IRegistroDeEvento>();
        var falhou = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        assinante.Setup(x => x.AssinarAsync(manipulador, It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                falhou.TrySetResult();
                return Task.FromException(new IOException("Broker indisponível"));
            });
        var sut = new ConsumidorConsolidadoWorker(assinante.Object, manipulador, registro.Object);
        await sut.StartAsync(CancellationToken.None);
        await falhou.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await sut.StopAsync(CancellationToken.None);
        assinante.Verify(x => x.AssinarAsync(manipulador, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Worker_DeveFinalizarSemErro_QuandoForCanceladoDuranteConsumo()
    {
        var assinante = new Mock<IAssinante>();
        var manipulador = Mock.Of<IManipuladorDeMensagem>();
        var registro = new Mock<IRegistroDeEvento>();
        var consumoIniciado = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        assinante
            .Setup(x => x.AssinarAsync(manipulador, It.IsAny<CancellationToken>()))
            .Returns<IManipuladorDeMensagem, CancellationToken>(async (_, token) =>
            {
                consumoIniciado.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            });
        var sut = new ConsumidorConsolidadoWorker(assinante.Object, manipulador, registro.Object);

        await sut.StartAsync(CancellationToken.None);
        await consumoIniciado.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var acao = () => sut.StopAsync(CancellationToken.None);

        await acao.Should().NotThrowAsync();
        registro.Verify(x => x.Informacao("Consumidor do consolidado diário finalizado.", It.IsAny<object?[]>()), Times.Once);
    }
}
