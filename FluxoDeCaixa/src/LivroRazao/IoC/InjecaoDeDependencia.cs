using Seguranca.Swagger;
using FluentValidation;
using LivroRazao.Aplicacao.Abstracao;
using LivroRazao.Aplicacao.Servico;
using LivroRazao.Aplicacao.Validacao;
using LivroRazao.Dominio.Caixa;
using LivroRazao.Infraestrutura.Persistencia;
using LivroRazao.Infraestrutura.RegistroDeEvento;
using LivroRazao.Infraestrutura.Repositorio;
using LivroRazao.Infraestrutura.Worker;
using Mensageria.RabbitMQ;
using Microsoft.EntityFrameworkCore;

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
            ?? throw new InvalidOperationException(
                "A connection string 'LivroRazao' nao foi configurada."
                );

        services.AddDbContext<LivroRazaoContexto>(
            options => options.UseNpgsql(connectionString)
            );

        services
            .AddOptions<PublicadorOutboxConfiguracao>()
            .Bind(
                configuration.GetSection(
                    PublicadorOutboxConfiguracao.Secao
                    )
                )
            .Validate(
                configuracao =>
                    configuracao.IntervaloEmSegundos > 0,
                "IntervaloEmSegundos deve ser maior que zero."
                )
            .Validate(
                configuracao =>
                    configuracao.IntervaloDeAlertaDeTentativas > 0,
                "IntervaloDeAlertaDeTentativas deve ser maior que zero."
                )
            .Validate(
                configuracao =>
                    configuracao.TempoLimiteDeProcessamentoEmMinutos > 0,
                "TempoLimiteDeProcessamentoEmMinutos deve ser maior que zero."
                )
            .Validate(
                configuracao =>
                    configuracao.QuantidadePorLote > 0,
                "QuantidadePorLote deve ser maior que zero."
                )
            .ValidateOnStart();

        services.AdicionarMensageriaRabbitMq(configuration);

        services.AddValidatorsFromAssemblyContaining<
            CriarLancamentoValidador
            >();

        services.AddScoped<
            IUnidadeDeTrabalho,
            UnidadeDeTrabalho
            >();

        services.AddScoped<
            ILancamentoRepositorio,
            LancamentoRepositorio
            >();

        services.AddScoped<
            ILancamentoOutboxRepositorio,
            LancamentoOutboxRepositorio
            >();

        services.AddScoped<
            ILancamentoServico,
            LancamentoServico
            >();

        services.AddSingleton<
            IRegistroDeEvento,
            RegistroDeEvento
            >();

        services.AddScoped<
            IProcessadorOutbox,
            ProcessadorOutbox
            >();

        services.AddHostedService<PublicadorOutboxWorker>();

        services.AddEndpointsApiExplorer();
        services.AdicionarSwaggerComSeguranca();

        services
            .AddHealthChecks()
            .AddDbContextCheck<LivroRazaoContexto>(
                "postgresql"
                );

        return services;
    }
}
