using System.Reflection;
using ConsolidadoDiario.Infraestrutura.Mensageria;
using FluentAssertions;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Xunit;

namespace ConsolidadoDiario.TesteDeUnidade.Infraestrutura.Mensageria;

public sealed class RabbitMqConexaoTestes
{
    [Fact]
    public void DeveConfigurarFabricaDeConexao()
    {
        var configuracao = new RabbitMqConfiguracao
        {
            Host = "rabbitmq",
            Porta = 5673,
            Usuario = "usuario",
            Senha = "senha",
            VirtualHost = "/consolidado"
        };

        using var conexao = new RabbitMqConexao(Options.Create(configuracao));

        var fabrica = ObterFabricaDeConexao(conexao);

        fabrica.HostName.Should().Be(configuracao.Host);
        fabrica.Port.Should().Be(configuracao.Porta);
        fabrica.UserName.Should().Be(configuracao.Usuario);
        fabrica.Password.Should().Be(configuracao.Senha);
        fabrica.VirtualHost.Should().Be(configuracao.VirtualHost);
        fabrica.AutomaticRecoveryEnabled.Should().BeTrue();
        fabrica.TopologyRecoveryEnabled.Should().BeTrue();
        fabrica.DispatchConsumersAsync.Should().BeTrue();
    }

    [Fact]
    public void DevePermitirDescartarMaisDeUmaVez()
    {
        var conexao = new RabbitMqConexao(Options.Create(new RabbitMqConfiguracao()));

        conexao.Dispose();
        Action acao = conexao.Dispose;

        acao.Should().NotThrow();
    }

    [Fact]
    public void DeveRejeitarObtencaoDeConexaoAposDescarte()
    {
        var conexao = new RabbitMqConexao(Options.Create(new RabbitMqConfiguracao()));
        conexao.Dispose();

        Func<IConnection> acao = conexao.ObterConexao;

        acao.Should()
            .Throw<ObjectDisposedException>();
    }

    [Fact]
    public void DeveRetornarConexaoAbertaExistente()
    {
        using var rabbitMqConexao = new RabbitMqConexao(Options.Create(new RabbitMqConfiguracao()));
        var conexao = new Moq.Mock<IConnection>();
        conexao.SetupGet(x => x.IsOpen).Returns(true);
        DefinirConexao(rabbitMqConexao, conexao.Object);

        var resultado = rabbitMqConexao.ObterConexao();

        resultado.Should().BeSameAs(conexao.Object);
    }

    [Fact]
    public void DeveFecharEDescartarConexaoAberta()
    {
        var rabbitMqConexao = new RabbitMqConexao(Options.Create(new RabbitMqConfiguracao()));
        var conexao = new Moq.Mock<IConnection>();
        conexao.SetupGet(x => x.IsOpen).Returns(true);
        DefinirConexao(rabbitMqConexao, conexao.Object);

        rabbitMqConexao.Dispose();

        conexao.Verify(x => x.Close(), Moq.Times.Once);
        conexao.Verify(x => x.Dispose(), Moq.Times.Once);
    }

    [Fact]
    public void DeveDescartarConexaoFechadaSemTentarFecharNovamente()
    {
        var rabbitMqConexao = new RabbitMqConexao(Options.Create(new RabbitMqConfiguracao()));
        var conexao = new Moq.Mock<IConnection>();
        conexao.SetupGet(x => x.IsOpen).Returns(false);
        DefinirConexao(rabbitMqConexao, conexao.Object);

        rabbitMqConexao.Dispose();

        conexao.Verify(x => x.Close(), Moq.Times.Never);
        conexao.Verify(x => x.Dispose(), Moq.Times.Once);
    }

    [Fact]
    public void DeveDescartarConexaoQuandoFechamentoFalhar()
    {
        var rabbitMqConexao = new RabbitMqConexao(Options.Create(new RabbitMqConfiguracao()));
        var conexao = new Moq.Mock<IConnection>();
        conexao.SetupGet(x => x.IsOpen).Returns(true);
        conexao.Setup(x => x.Close()).Throws(new InvalidOperationException("Falha."));
        DefinirConexao(rabbitMqConexao, conexao.Object);

        Action acao = rabbitMqConexao.Dispose;

        acao.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("Falha.");
        conexao.Verify(x => x.Dispose(), Moq.Times.Once);
    }

    private static ConnectionFactory ObterFabricaDeConexao(RabbitMqConexao conexao)
    {
        return (ConnectionFactory)typeof(RabbitMqConexao)
            .GetField("_fabricaDeConexao", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(conexao)!;
    }

    private static void DefinirConexao(
        RabbitMqConexao rabbitMqConexao,
        IConnection conexao)
    {
        typeof(RabbitMqConexao)
            .GetField("_conexao", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(rabbitMqConexao, conexao);
    }
}
