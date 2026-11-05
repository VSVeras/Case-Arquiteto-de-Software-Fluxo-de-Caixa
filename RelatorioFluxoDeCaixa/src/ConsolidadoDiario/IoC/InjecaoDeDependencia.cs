using Seguranca.Swagger;
using ConsolidadoDiario.Aplicacao.Abstracao;
using ConsolidadoDiario.Aplicacao.Servico;
using ConsolidadoDiario.Infraestrutura.Dao;
using ConsolidadoDiario.Infraestrutura.RegistroDeEvento;
using ConsolidadoDiario.Infraestrutura.Worker;
using Mensageria.Assinante;
using Mensageria.RabbitMQ;
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
            ?? throw new InvalidOperationException(
                "A connection string 'ConsolidadoDiario' não foi configurada.");

        services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));

        services.AdicionarMensageriaRabbitMq(configuration);

        services.AddScoped<IConsolidadoDiarioDao, ConsolidadoDiarioDao>();
        services.AddScoped<IProcessadorEventoLancamento, ProcessadorEventoLancamento>();
        services.AddScoped<IConsultaConsolidadoDiarioServico, ConsultaConsolidadoDiarioServico>();
        services.AddSingleton<IRegistroDeEvento, RegistroDeEvento>();
        services.AddSingleton<IManipuladorDeMensagem, ManipuladorEventoLancamento>();
        services.AddHostedService<ConsumidorConsolidadoWorker>();

        services.AddEndpointsApiExplorer();
        services.AdicionarSwaggerComSeguranca();

        services
            .AddHealthChecks()
            .AddNpgSql(connectionString, name: "postgresql");

        return services;
    }
}
