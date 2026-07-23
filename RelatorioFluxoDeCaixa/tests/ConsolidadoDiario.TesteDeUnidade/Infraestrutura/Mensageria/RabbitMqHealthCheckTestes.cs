using ConsolidadoDiario.Infraestrutura.Mensageria;
using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using RabbitMQ.Client;
using Xunit;

namespace ConsolidadoDiario.TesteDeUnidade.Infraestrutura.Mensageria;

public sealed class RabbitMqHealthCheckTestes
{
    [Fact]
    public async Task DeveRetornarSaudavelQuandoConexaoEstiverAberta()
    {
        var conexao = new Mock<IConnection>();
        conexao.SetupGet(x => x.IsOpen).Returns(true);

        var rabbitMqConexao = new Mock<IRabbitMqConexao>();
        rabbitMqConexao.Setup(x => x.ObterConexao()).Returns(conexao.Object);

        var healthCheck = new RabbitMqHealthCheck(rabbitMqConexao.Object);

        var resultado = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        resultado.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task DeveRetornarNaoSaudavelQuandoConexaoEstiverFechada()
    {
        var conexao = new Mock<IConnection>();
        conexao.SetupGet(x => x.IsOpen).Returns(false);

        var rabbitMqConexao = new Mock<IRabbitMqConexao>();
        rabbitMqConexao.Setup(x => x.ObterConexao()).Returns(conexao.Object);

        var healthCheck = new RabbitMqHealthCheck(rabbitMqConexao.Object);

        var resultado = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        resultado.Status.Should().Be(HealthStatus.Unhealthy);
        resultado.Description.Should().Be("A conexão com o RabbitMQ está fechada.");
    }

    [Fact]
    public async Task DeveRetornarNaoSaudavelQuandoConexaoFalhar()
    {
        var excecao = new InvalidOperationException("Falha de conexão.");
        var rabbitMqConexao = new Mock<IRabbitMqConexao>();
        rabbitMqConexao.Setup(x => x.ObterConexao()).Throws(excecao);

        var healthCheck = new RabbitMqHealthCheck(rabbitMqConexao.Object);

        var resultado = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        resultado.Status.Should().Be(HealthStatus.Unhealthy);
        resultado.Description.Should().Be("Não foi possível conectar ao RabbitMQ.");
        resultado.Exception.Should().BeSameAs(excecao);
    }

    [Fact]
    public async Task DeveRetornarNaoSaudavelQuandoCancelado()
    {
        var rabbitMqConexao = new Mock<IRabbitMqConexao>();
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        var healthCheck = new RabbitMqHealthCheck(rabbitMqConexao.Object);

        var resultado = await healthCheck.CheckHealthAsync(
            new HealthCheckContext(),
            cancellationTokenSource.Token);

        resultado.Status.Should().Be(HealthStatus.Unhealthy);
        resultado.Exception.Should().BeOfType<OperationCanceledException>();
        rabbitMqConexao.Verify(x => x.ObterConexao(), Times.Never);
    }
}
