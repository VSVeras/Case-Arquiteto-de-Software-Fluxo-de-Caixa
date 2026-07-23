using ConsolidadoDiario.Infraestrutura.Mensageria;
using Microsoft.Extensions.Options;
using Moq;
using RabbitMQ.Client;
using Xunit;

namespace ConsolidadoDiario.TesteDeUnidade.Infraestrutura.Mensageria;

public sealed class InicializadorRabbitMqTestes
{
    [Fact]
    public void DeveDeclararExchangeFilaEVinculo()
    {
        var canal = new Mock<IModel>();
        var conexao = new Mock<IConnection>();
        conexao.Setup(x => x.CreateModel()).Returns(canal.Object);

        var rabbitMqConexao = new Mock<IRabbitMqConexao>();
        rabbitMqConexao.Setup(x => x.ObterConexao()).Returns(conexao.Object);

        var configuracao = new RabbitMqConfiguracao
        {
            Exchange = "exchange.teste",
            FilaLancamentoCriado = "fila.teste",
            RoutingKeyLancamentoCriado = "routing.teste"
        };

        var inicializador = new InicializadorRabbitMq(
            rabbitMqConexao.Object,
            Options.Create(configuracao));

        inicializador.Inicializar();

        canal.Verify(x => x.ExchangeDeclare(
            "exchange.teste",
            ExchangeType.Topic,
            true,
            false,
            null), Times.Once);

        canal.Verify(x => x.QueueDeclare(
            "fila.teste",
            true,
            false,
            false,
            null), Times.Once);

        canal.Verify(x => x.QueueBind(
            "fila.teste",
            "exchange.teste",
            "routing.teste",
            null), Times.Once);

        canal.Verify(x => x.Dispose(), Times.Once);
    }
}
