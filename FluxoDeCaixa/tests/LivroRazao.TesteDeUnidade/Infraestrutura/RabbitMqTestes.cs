using System.Text;
using FluentAssertions;
using LivroRazao.Infraestrutura.Mensageria;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Moq;
using RabbitMQ.Client;
using Xunit;

namespace LivroRazao.TesteDeUnidade.Infraestrutura;

public sealed class RabbitMqTestes
{
    [Fact]
    public void InicializadorDeveDeclararTopologiaComDeadLetterQueue()
    {
        var canal = new Mock<IModel>();
        var conexao = new Mock<IConnection>();
        var fabricaDeConexao = new Mock<IRabbitMqConexao>();
        var configuracao = CriarConfiguracao();

        conexao
            .Setup(x => x.CreateModel())
            .Returns(canal.Object);

        fabricaDeConexao
            .Setup(x => x.ObterConexao())
            .Returns(conexao.Object);

        var inicializador = new InicializadorRabbitMq(
            fabricaDeConexao.Object,
            Options.Create(configuracao)
            );

        inicializador.Inicializar();

        canal.Verify(
            x => x.ExchangeDeclare(
                configuracao.Exchange,
                ExchangeType.Topic,
                true,
                false,
                null
                ),
            Times.Once
            );

        canal.Verify(
            x => x.ExchangeDeclare(
                configuracao.DeadLetterExchange,
                ExchangeType.Direct,
                true,
                false,
                null
                ),
            Times.Once
            );

        canal.Verify(
            x => x.QueueDeclare(
                configuracao.FilaLancamentoCriadoDlq,
                true,
                false,
                false,
                null
                ),
            Times.Once
            );

        canal.Verify(
            x => x.QueueBind(
                configuracao.FilaLancamentoCriadoDlq,
                configuracao.DeadLetterExchange,
                configuracao.RoutingKeyLancamentoCriadoDlq,
                null
                ),
            Times.Once
            );

        canal.Verify(
            x => x.QueueDeclare(
                configuracao.FilaLancamentoCriado,
                true,
                false,
                false,
                It.Is<IDictionary<string, object>>(
                    argumentos =>
                        argumentos.Count == 2
                        && argumentos[
                            "x-dead-letter-exchange"
                            ].Equals(
                                configuracao.DeadLetterExchange
                                )
                        && argumentos[
                            "x-dead-letter-routing-key"
                            ].Equals(
                                configuracao
                                    .RoutingKeyLancamentoCriadoDlq
                                )
                    )
                ),
            Times.Once
            );

        canal.Verify(
            x => x.QueueBind(
                configuracao.FilaLancamentoCriado,
                configuracao.Exchange,
                configuracao.RoutingKeyLancamentoCriado,
                null
                ),
            Times.Once
            );
    }

    [Fact]
    public async Task PublicadorDevePublicarEventoPersistente()
    {
        var propriedades = new Mock<IBasicProperties>();
        var canal = new Mock<IModel>();
        var conexao = new Mock<IConnection>();
        var fabricaDeConexao = new Mock<IRabbitMqConexao>();
        var configuracao = CriarConfiguracao();
        var correlationId = Guid.NewGuid();

        canal
            .Setup(x => x.CreateBasicProperties())
            .Returns(propriedades.Object);

        conexao
            .Setup(x => x.CreateModel())
            .Returns(canal.Object);

        fabricaDeConexao
            .Setup(x => x.ObterConexao())
            .Returns(conexao.Object);

        var publicador = new PublicadorRabbitMq(
            fabricaDeConexao.Object,
            Options.Create(configuracao)
            );

        await publicador.PublicarAsync(
            "EventoTeste",
            "{}",
            correlationId,
            CancellationToken.None
            );

        propriedades.VerifySet(
            x => x.Persistent = true
            );

        propriedades.VerifySet(
            x => x.CorrelationId = correlationId.ToString()
            );

        propriedades.VerifySet(
            x => x.Type = "EventoTeste"
            );

        propriedades.VerifySet(
            x => x.ContentType = "application/json"
            );

        canal.Verify(
            x => x.ConfirmSelect(),
            Times.Once
            );

        canal.Verify(
            x => x.BasicPublish(
                configuracao.Exchange,
                configuracao.RoutingKeyLancamentoCriado,
                true,
                propriedades.Object,
                It.Is<ReadOnlyMemory<byte>>(
                    corpo =>
                        Encoding.UTF8.GetString(
                            corpo.ToArray()
                            ) == "{}"
                    )
                ),
            Times.Once
            );

        canal.Verify(
            x => x.WaitForConfirmsOrDie(
                TimeSpan.FromSeconds(10)
                ),
            Times.Once
            );
    }

    [Fact]
    public async Task PublicadorDeveRespeitarCancelamento()
    {
        var publicador = new PublicadorRabbitMq(
            Mock.Of<IRabbitMqConexao>(),
            Options.Create(CriarConfiguracao())
            );

        using var cancelamento =
            new CancellationTokenSource();

        cancelamento.Cancel();

        var acao = () => publicador.PublicarAsync(
            "EventoTeste",
            "{}",
            Guid.NewGuid(),
            cancelamento.Token
            );

        await acao
            .Should()
            .ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task HealthCheckDeveRetornarSaudavel()
    {
        var canal = new Mock<IModel>();
        var conexao = new Mock<IConnection>();
        var fabricaDeConexao = new Mock<IRabbitMqConexao>();

        canal
            .SetupGet(x => x.IsOpen)
            .Returns(true);

        conexao
            .SetupGet(x => x.IsOpen)
            .Returns(true);

        conexao
            .Setup(x => x.CreateModel())
            .Returns(canal.Object);

        fabricaDeConexao
            .Setup(x => x.ObterConexao())
            .Returns(conexao.Object);

        var healthCheck = new RabbitMqHealthCheck(
            fabricaDeConexao.Object,
            Options.Create(CriarConfiguracao())
            );

        var resultado =
            await healthCheck.CheckHealthAsync(
                new HealthCheckContext()
                );

        resultado.Status
            .Should()
            .Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task HealthCheckDeveRetornarNaoSaudavelQuandoConexaoEstiverFechada()
    {
        var conexao = new Mock<IConnection>();
        var fabricaDeConexao = new Mock<IRabbitMqConexao>();

        conexao
            .SetupGet(x => x.IsOpen)
            .Returns(false);

        fabricaDeConexao
            .Setup(x => x.ObterConexao())
            .Returns(conexao.Object);

        var healthCheck = new RabbitMqHealthCheck(
            fabricaDeConexao.Object,
            Options.Create(CriarConfiguracao())
            );

        var resultado =
            await healthCheck.CheckHealthAsync(
                new HealthCheckContext()
                );

        resultado.Status
            .Should()
            .Be(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task HealthCheckDeveRetornarNaoSaudavelQuandoCanalEstiverFechado()
    {
        var canal = new Mock<IModel>();
        var conexao = new Mock<IConnection>();
        var fabricaDeConexao = new Mock<IRabbitMqConexao>();

        canal
            .SetupGet(x => x.IsOpen)
            .Returns(false);

        conexao
            .SetupGet(x => x.IsOpen)
            .Returns(true);

        conexao
            .Setup(x => x.CreateModel())
            .Returns(canal.Object);

        fabricaDeConexao
            .Setup(x => x.ObterConexao())
            .Returns(conexao.Object);

        var healthCheck = new RabbitMqHealthCheck(
            fabricaDeConexao.Object,
            Options.Create(CriarConfiguracao())
            );

        var resultado =
            await healthCheck.CheckHealthAsync(
                new HealthCheckContext()
                );

        resultado.Status
            .Should()
            .Be(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task HealthCheckDeveRetornarNaoSaudavelQuandoOcorrerFalha()
    {
        var fabricaDeConexao =
            new Mock<IRabbitMqConexao>();

        fabricaDeConexao
            .Setup(x => x.ObterConexao())
            .Throws(
                new InvalidOperationException("Falha.")
                );

        var healthCheck = new RabbitMqHealthCheck(
            fabricaDeConexao.Object,
            Options.Create(CriarConfiguracao())
            );

        var resultado =
            await healthCheck.CheckHealthAsync(
                new HealthCheckContext()
                );

        resultado.Status
            .Should()
            .Be(HealthStatus.Unhealthy);

        resultado.Exception
            .Should()
            .BeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task HealthCheckDeveRespeitarCancelamento()
    {
        var healthCheck = new RabbitMqHealthCheck(
            Mock.Of<IRabbitMqConexao>(),
            Options.Create(CriarConfiguracao())
            );

        using var cancelamento =
            new CancellationTokenSource();

        cancelamento.Cancel();

        var acao = () =>
            healthCheck.CheckHealthAsync(
                new HealthCheckContext(),
                cancelamento.Token
                );

        await acao
            .Should()
            .ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void ConexaoDescartadaNaoDevePermitirNovaConexao()
    {
        var conexao = new RabbitMqConexao(
            Options.Create(CriarConfiguracao())
            );

        conexao.Dispose();

        var acao = () => conexao.ObterConexao();

        acao
            .Should()
            .Throw<ObjectDisposedException>();
    }

    private static RabbitMqConfiguracao CriarConfiguracao()
    {
        return new RabbitMqConfiguracao
        {
            Host = "localhost",
            Porta = 5672,
            Usuario = "guest",
            Senha = "guest",
            VirtualHost = "/",
            Exchange = "exchange.teste",
            RoutingKeyLancamentoCriado = "routing.teste",
            FilaLancamentoCriado = "fila.teste",
            DeadLetterExchange = "exchange.teste.dlx",
            FilaLancamentoCriadoDlq = "fila.teste.dlq",
            RoutingKeyLancamentoCriadoDlq =
                "routing.teste.dlq"
        };
    }
}
