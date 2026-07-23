using ConsolidadoDiario.Aplicacao.Abstracao;
using ConsolidadoDiario.Aplicacao.Servico;
using ConsolidadoDiario.Infraestrutura.Dao;
using ConsolidadoDiario.Infraestrutura.Mensageria;
using ConsolidadoDiario.Infraestrutura.RegistroDeEvento;
using ConsolidadoDiario.Infraestrutura.Worker;
using ConsolidadoDiario.IoC;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace ConsolidadoDiario.TesteDeUnidade.IoC;

public sealed class InjecaoDeDependenciaTestes
{
    [Fact]
    public void DeveRejeitarConfiguracaoSemConnectionString()
    {
        var configuracao = new ConfigurationBuilder().Build();
        var servicos = new ServiceCollection();

        var acao = () => servicos.AdicionarDependenciasDaAplicacao(configuracao);

        acao.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("A connection string 'ConsolidadoDiario' não foi configurada.");
    }

    [Fact]
    public void DeveRegistrarDependenciasDaAplicacao()
    {
        var configuracao = CriarConfiguracao();
        var servicos = new ServiceCollection();

        var resultado = servicos.AdicionarDependenciasDaAplicacao(configuracao);

        resultado.Should().BeSameAs(servicos);
        servicos.Should().Contain(x =>
            x.ServiceType == typeof(IConsolidadoDiarioDao)
            && x.ImplementationType == typeof(ConsolidadoDiarioDao)
            && x.Lifetime == ServiceLifetime.Scoped);
        servicos.Should().Contain(x =>
            x.ServiceType == typeof(IProcessadorEventoLancamento)
            && x.ImplementationType == typeof(ProcessadorEventoLancamento)
            && x.Lifetime == ServiceLifetime.Scoped);
        servicos.Should().Contain(x =>
            x.ServiceType == typeof(IConsultaConsolidadoDiarioServico)
            && x.ImplementationType == typeof(ConsultaConsolidadoDiarioServico)
            && x.Lifetime == ServiceLifetime.Scoped);
        servicos.Should().Contain(x =>
            x.ServiceType == typeof(IRabbitMqConexao)
            && x.ImplementationType == typeof(RabbitMqConexao)
            && x.Lifetime == ServiceLifetime.Singleton);
        servicos.Should().Contain(x =>
            x.ServiceType == typeof(IInicializadorRabbitMq)
            && x.ImplementationType == typeof(InicializadorRabbitMq)
            && x.Lifetime == ServiceLifetime.Singleton);
        servicos.Should().Contain(x =>
            x.ServiceType == typeof(IRegistroDeEvento)
            && x.ImplementationType == typeof(RegistroDeEvento)
            && x.Lifetime == ServiceLifetime.Singleton);
        servicos.Should().Contain(x =>
            x.ServiceType == typeof(IHostedService)
            && x.ImplementationType == typeof(ConsumidorConsolidadoWorker));
    }

    [Theory]
    [InlineData("RabbitMq:Host", "", "RabbitMq:Host deve ser informado.")]
    [InlineData("RabbitMq:Porta", "0", "RabbitMq:Porta deve ser maior que zero.")]
    [InlineData("RabbitMq:Exchange", "", "RabbitMq:Exchange deve ser informado.")]
    [InlineData("RabbitMq:RoutingKeyLancamentoCriado", "", "RabbitMq:RoutingKeyLancamentoCriado deve ser informado.")]
    [InlineData("RabbitMq:FilaLancamentoCriado", "", "RabbitMq:FilaLancamentoCriado deve ser informada.")]
    [InlineData("RabbitMq:QuantidadeDeMensagensEmProcessamento", "0", "RabbitMq:QuantidadeDeMensagensEmProcessamento deve ser maior que zero.")]
    public void DeveRejeitarConfiguracaoRabbitMqInvalida(
        string chave,
        string valor,
        string mensagemEsperada)
    {
        var valores = CriarValoresDeConfiguracao();
        valores[chave] = valor;
        var configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(valores)
            .Build();
        var servicos = new ServiceCollection();
        servicos.AdicionarDependenciasDaAplicacao(configuracao);
        using var provedor = servicos.BuildServiceProvider();

        var acao = () => provedor.GetRequiredService<IOptions<RabbitMqConfiguracao>>().Value;

        acao.Should()
            .Throw<OptionsValidationException>()
            .Where(x => x.Failures.Contains(mensagemEsperada));
    }

    private static IConfiguration CriarConfiguracao()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(CriarValoresDeConfiguracao())
            .Build();
    }

    private static Dictionary<string, string?> CriarValoresDeConfiguracao()
    {
        return new Dictionary<string, string?>
        {
            ["ConnectionStrings:ConsolidadoDiario"] = "Host=localhost;Database=consolidado;Username=postgres;Password=postgres",
            ["RabbitMq:Host"] = "localhost",
            ["RabbitMq:Porta"] = "5672",
            ["RabbitMq:Usuario"] = "guest",
            ["RabbitMq:Senha"] = "guest",
            ["RabbitMq:VirtualHost"] = "/",
            ["RabbitMq:Exchange"] = "fluxocaixa.exchange",
            ["RabbitMq:RoutingKeyLancamentoCriado"] = "fluxocaixa.lancamento.criado",
            ["RabbitMq:FilaLancamentoCriado"] = "fluxocaixa.lancamento.criado",
            ["RabbitMq:QuantidadeDeMensagensEmProcessamento"] = "1"
        };
    }
}
