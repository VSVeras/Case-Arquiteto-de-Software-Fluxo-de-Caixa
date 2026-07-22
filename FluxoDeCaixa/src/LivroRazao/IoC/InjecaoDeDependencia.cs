using FluentValidation;
using LivroRazao.Aplicacao.Abstracao;
using LivroRazao.Aplicacao.Servico;
using LivroRazao.Aplicacao.Validacao;
using LivroRazao.Dominio.Caixa;
using LivroRazao.Infraestrutura.Mensageria;
using LivroRazao.Infraestrutura.Persistencia;
using LivroRazao.Infraestrutura.RegistroDeEvento;
using LivroRazao.Infraestrutura.Repositorio;
using LivroRazao.Infraestrutura.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LivroRazao.IoC;

public static class InjecaoDeDependencia
{
    public static IServiceCollection AdicionarDependenciasDaAplicacao(
        this IServiceCollection services,
        IConfiguration configuration
        )
    {
        var connectionString =
            configuration.GetConnectionString("LivroRazao")
            ?? throw new InvalidOperationException("A connection string 'LivroRazao' nao foi configurada.");

        services.AddDbContext<LivroRazaoContexto>(options => options.UseNpgsql(connectionString));

        services
            .AddOptions<PublicadorOutboxConfiguracao>()
            .Bind(configuration.GetSection(PublicadorOutboxConfiguracao.Secao))
            .Validate(configuracao => configuracao.IntervaloEmSegundos > 0, "IntervaloEmSegundos deve ser maior que zero.")
            .Validate(configuracao => configuracao.LimiteDeTentativas > 0, "LimiteDeTentativas deve ser maior que zero.")
            .Validate(configuracao => configuracao.TempoLimiteDeProcessamentoEmMinutos > 0, "TempoLimiteDeProcessamentoEmMinutos deve ser maior que zero.")
            .ValidateOnStart();

        services
            .AddOptions<RabbitMqConfiguracao>()
            .Bind(configuration.GetSection(RabbitMqConfiguracao.Secao))
            .Validate(configuracao => !string.IsNullOrWhiteSpace(configuracao.Host), "RabbitMq:Host deve ser informado.")
            .Validate(configuracao => configuracao.Porta > 0, "RabbitMq:Porta deve ser maior que zero.")
            .Validate(configuracao => !string.IsNullOrWhiteSpace(configuracao.Exchange), "RabbitMq:Exchange deve ser informado.")
            .Validate(configuracao => !string.IsNullOrWhiteSpace(configuracao.RoutingKeyLancamentoCriado), "RabbitMq:RoutingKeyLancamentoCriado deve ser informado.")
            .Validate(configuracao => !string.IsNullOrWhiteSpace(configuracao.FilaLancamentoCriado), "RabbitMq:FilaLancamentoCriado deve ser informada.")
            .ValidateOnStart();

        services.AddValidatorsFromAssemblyContaining<CriarLancamentoValidador>();

        services.AddScoped<IUnidadeDeTrabalho, UnidadeDeTrabalho>();
        services.AddScoped<ILancamentoRepositorio, LancamentoRepositorio>();
        services.AddScoped<ILancamentoOutboxRepositorio, LancamentoOutboxRepositorio>();
        services.AddScoped<ILancamentoServico, LancamentoServico>();

        services.AddSingleton<IRabbitMqConexao, RabbitMqConexao>();
        services.AddSingleton<IInicializadorRabbitMq, InicializadorRabbitMq>();
        services.AddSingleton<IPublicadorDeEventos, PublicadorRabbitMq>();

        services.AddSingleton<IRegistroDeEvento, RegistroDeEvento>();

        services.AddScoped<IProcessadorOutbox, ProcessadorOutbox>();
        services.AddHostedService<PublicadorOutboxWorker>();

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        services
            .AddHealthChecks()
            .AddDbContextCheck<LivroRazaoContexto>("postgresql")
            .AddCheck<RabbitMqHealthCheck>(
                name: "rabbitmq",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["ready"],
                timeout: TimeSpan.FromSeconds(5)
                );

        return services;
    }
}
