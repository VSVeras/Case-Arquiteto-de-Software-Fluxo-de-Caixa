using ConsolidadoDiario.Infraestrutura.Mensageria;
using Microsoft.Extensions.Options;
using Moq;
using RabbitMQ.Client;
using Xunit;

namespace ConsolidadoDiario.TesteDeUnidade.Infraestrutura.Mensageria;

public sealed class InicializadorRabbitMqTestes
{
    [Fact]
    public void DeveValidarPassivamenteExchangeEFila()
    {
        var canal = new Mock<IModel>();
        var conexao = new Mock<IConnection>();

        conexao
            .Setup(x => x.CreateModel())
            .Returns(canal.Object);

        var rabbitMqConexao =
            new Mock<IRabbitMqConexao>();

        rabbitMqConexao
            .Setup(x => x.ObterConexao())
            .Returns(conexao.Object);

        var configuracao = new RabbitMqConfiguracao
        {
            Exchange = "exchange.teste",
            FilaLancamentoCriado = "fila.teste",
            RoutingKeyLancamentoCriado = "routing.teste"
        };

        var inicializador = new InicializadorRabbitMq(
            rabbitMqConexao.Object,
            Options.Create(configuracao)
        );

        inicializador.Inicializar();

        canal.Verify(
            x => x.ExchangeDeclarePassive(
                "exchange.teste"
            ),
            Times.Once
        );

        canal.Verify(
            x => x.QueueDeclarePassive(
                "fila.teste"
            ),
            Times.Once
        );

        canal.Verify(
            x => x.ExchangeDeclare(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<IDictionary<string, object>?>()
            ),
            Times.Never
        );

        canal.Verify(
            x => x.QueueDeclare(
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<IDictionary<string, object>?>()
            ),
            Times.Never
        );

        canal.Verify(
            x => x.QueueBind(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IDictionary<string, object>?>()
            ),
            Times.Never
        );

        canal.Verify(
            x => x.Dispose(),
            Times.Once
        );
    }
}
