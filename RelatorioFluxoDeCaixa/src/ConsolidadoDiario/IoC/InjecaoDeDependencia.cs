using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ConsolidadoDiario.IoC;

public static class InjecaoDeDependencia
{
    public static IServiceCollection AdicionarDependenciasDaAplicacao(
        this IServiceCollection services,
        IConfiguration configuration
        )
    {
        return services;
    }
}
