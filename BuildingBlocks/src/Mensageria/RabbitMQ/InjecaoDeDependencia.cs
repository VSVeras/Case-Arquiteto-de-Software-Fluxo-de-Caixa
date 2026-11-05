using Mensageria.Assinante;
using Mensageria.Produtor;
using Mensageria.RabbitMQ.Configuracao;
using Mensageria.RabbitMQ.Conexao;
using Mensageria.RabbitMQ.HealthCheck;
using Mensageria.RabbitMQ.Produtor;
using Mensageria.RabbitMQ.Topologia;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Mensageria.RabbitMQ;

public static class InjecaoDeDependencia
{
    public static IServiceCollection AdicionarMensageriaRabbitMq(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<RabbitMqConfiguracao>()
            .Bind(configuration.GetSection(RabbitMqConfiguracao.Secao))
            .Validate(c => !string.IsNullOrWhiteSpace(c.Host), "RabbitMq:Host deve ser informado.")
            .Validate(c => c.Porta > 0, "RabbitMq:Porta deve ser maior que zero.")
            .Validate(c => !string.IsNullOrWhiteSpace(c.Usuario), "RabbitMq:Usuario deve ser informado.")
            .Validate(c => !string.IsNullOrWhiteSpace(c.Senha), "RabbitMq:Senha deve ser informada.")
            .Validate(c => !string.IsNullOrWhiteSpace(c.VirtualHost), "RabbitMq:VirtualHost deve ser informado.")
            .Validate(c => !string.IsNullOrWhiteSpace(c.Exchange), "RabbitMq:Exchange deve ser informado.")
            .Validate(c => !string.IsNullOrWhiteSpace(c.RoutingKey), "RabbitMq:RoutingKey deve ser informado.")
            .Validate(c => !string.IsNullOrWhiteSpace(c.Fila), "RabbitMq:Fila deve ser informada.")
            .Validate(c => c.QuantidadeDeMensagensEmProcessamento > 0, "RabbitMq:QuantidadeDeMensagensEmProcessamento deve ser maior que zero.")
            .Validate(c => !string.IsNullOrWhiteSpace(c.RetryExchange), "RabbitMq:RetryExchange deve ser informado.")
            .Validate(c => !string.IsNullOrWhiteSpace(c.RetryRoutingKey), "RabbitMq:RetryRoutingKey deve ser informado.")
            .Validate(c => !string.IsNullOrWhiteSpace(c.RetryFila), "RabbitMq:RetryFila deve ser informada.")
            .Validate(c => c.RetryTempoEmMilissegundos > 0, "RabbitMq:RetryTempoEmMilissegundos deve ser maior que zero.")
            .Validate(c => c.QuantidadeMaximaDeTentativas > 0, "RabbitMq:QuantidadeMaximaDeTentativas deve ser maior que zero.")
            .Validate(c => !string.IsNullOrWhiteSpace(c.DeadLetterExchange), "RabbitMq:DeadLetterExchange deve ser informado.")
            .Validate(c => !string.IsNullOrWhiteSpace(c.DeadLetterRoutingKey), "RabbitMq:DeadLetterRoutingKey deve ser informado.")
            .Validate(c => !string.IsNullOrWhiteSpace(c.DeadLetterFila), "RabbitMq:DeadLetterFila deve ser informada.")
            .ValidateOnStart();

        services.AddSingleton<IRabbitMqConexao, RabbitMqConexao>();
        services.AddSingleton<IInicializadorRabbitMq, InicializadorRabbitMq>();
        services.AddSingleton<IPublicador, PublicadorRabbitMq>();
        services.AddSingleton<IAssinante, Assinante.AssinanteRabbitMq>();
        services.AddHealthChecks().AddCheck<RabbitMqHealthCheck>("rabbitmq");

        return services;
    }
}
