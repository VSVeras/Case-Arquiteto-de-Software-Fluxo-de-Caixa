using ConsolidadoDiario.Aplicacao.Abstracao;
using ConsolidadoDiario.Aplicacao.Servico;
using ConsolidadoDiario.Infraestrutura.Dao;
using ConsolidadoDiario.Infraestrutura.Mensageria;
using ConsolidadoDiario.Infraestrutura.RegistroDeEvento;
using ConsolidadoDiario.Infraestrutura.Worker;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace ConsolidadoDiario.IoC;

public static class InjecaoDeDependencia
{
    public static IServiceCollection AdicionarDependenciasDaAplicacao(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString("ConsolidadoDiario")
            ?? throw new InvalidOperationException("A connection string 'ConsolidadoDiario' não foi configurada.");

        services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));

        services
            .AddOptions<RabbitMqConfiguracao>()
            .Bind(configuration.GetSection(RabbitMqConfiguracao.Secao))
            .Validate(configuracao => !string.IsNullOrWhiteSpace(configuracao.Host), "RabbitMq:Host deve ser informado.")
            .Validate(configuracao => configuracao.Porta > 0, "RabbitMq:Porta deve ser maior que zero.")
            .Validate(configuracao => !string.IsNullOrWhiteSpace(configuracao.Exchange), "RabbitMq:Exchange deve ser informado.")
            .Validate(configuracao => !string.IsNullOrWhiteSpace(configuracao.RoutingKeyLancamentoCriado), "RabbitMq:RoutingKeyLancamentoCriado deve ser informado.")
            .Validate(configuracao => !string.IsNullOrWhiteSpace(configuracao.FilaLancamentoCriado), "RabbitMq:FilaLancamentoCriado deve ser informada.")
            .Validate(configuracao => configuracao.QuantidadeDeMensagensEmProcessamento > 0, "RabbitMq:QuantidadeDeMensagensEmProcessamento deve ser maior que zero.")
            .ValidateOnStart();

        services.AddScoped<IConsolidadoDiarioDao, ConsolidadoDiarioDao>();
        services.AddScoped<IProcessadorEventoLancamento, ProcessadorEventoLancamento>();
        services.AddScoped<IConsultaConsolidadoDiarioServico, ConsultaConsolidadoDiarioServico>();

        services.AddSingleton<IRabbitMqConexao, RabbitMqConexao>();
        services.AddSingleton<IInicializadorRabbitMq, InicializadorRabbitMq>();
        services.AddSingleton<IRegistroDeEvento, RegistroDeEvento>();
        services.AddSingleton<IPublicadorRetryRabbitMq, PublicadorRetryRabbitMq>();
        services.AddHostedService<ConsumidorConsolidadoWorker>();

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        services
            .AddHealthChecks()
            .AddNpgSql(connectionString, name: "postgresql")
            .AddCheck<RabbitMqHealthCheck>(
                name: "rabbitmq",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["ready"],
                timeout: TimeSpan.FromSeconds(5));

        return services;
    }
}
