using ConsolidadoDiario.Infraestrutura.Mensageria;
using Microsoft.Extensions.Options;
using Moq;
using RabbitMQ.Client;
using Xunit;

namespace ConsolidadoDiario.TesteDeUnidade.Infraestrutura.Mensageria;

public sealed class PublicadorRetryRabbitMqTestes
{
    [Fact]
    public void DevePublicarMensagemNaRetryExchangeEConfirmarPublicacao()
    {
        var configuracao = new RabbitMqConfiguracao
        {
            RetryExchange = "fluxocaixa.eventos.retry",
            RoutingKeyLancamentoCriadoRetry = "lancamento.criado.retry"
        };

        var canal = new Mock<IModel>();
        var propriedades = new Mock<IBasicProperties>();

        var corpo = new ReadOnlyMemory<byte>(
            [1, 2, 3, 4]);

        var publicador = new PublicadorRetryRabbitMq(
            Options.Create(configuracao));

        publicador.Publicar(
            canal.Object,
            corpo,
            propriedades.Object);

        canal.Verify(
            x => x.ConfirmSelect(),
            Times.Once);

        canal.Verify(
            x => x.BasicPublish(
                configuracao.RetryExchange,
                configuracao.RoutingKeyLancamentoCriadoRetry,
                false,
                propriedades.Object,
                corpo),
            Times.Once);

        canal.Verify(
            x => x.WaitForConfirmsOrDie(
                TimeSpan.FromSeconds(10)),
            Times.Once);
    }

    [Fact]
    public void DevePropagarFalhaQuandoRabbitMqNaoConfirmarPublicacao()
    {
        var configuracao = new RabbitMqConfiguracao
        {
            RetryExchange = "fluxocaixa.eventos.retry",
            RoutingKeyLancamentoCriadoRetry = "lancamento.criado.retry"
        };

        var canal = new Mock<IModel>();
        var propriedades = new Mock<IBasicProperties>();

        var corpo = new ReadOnlyMemory<byte>(
            [1, 2, 3, 4]);

        var excecaoEsperada = new InvalidOperationException(
            "A publicação não foi confirmada.");

        canal
            .Setup(x => x.WaitForConfirmsOrDie(
                TimeSpan.FromSeconds(10)))
            .Throws(excecaoEsperada);

        var publicador = new PublicadorRetryRabbitMq(
            Options.Create(configuracao));

        var excecaoRecebida = Assert.Throws<InvalidOperationException>(
            () => publicador.Publicar(
                canal.Object,
                corpo,
                propriedades.Object));

        Assert.Same(
            excecaoEsperada,
            excecaoRecebida);

        canal.Verify(
            x => x.ConfirmSelect(),
            Times.Once);

        canal.Verify(
            x => x.BasicPublish(
                configuracao.RetryExchange,
                configuracao.RoutingKeyLancamentoCriadoRetry,
                false,
                propriedades.Object,
                corpo),
            Times.Once);

        canal.Verify(
            x => x.WaitForConfirmsOrDie(
                TimeSpan.FromSeconds(10)),
            Times.Once);
    }
}
