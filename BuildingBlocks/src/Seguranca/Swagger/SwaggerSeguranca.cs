using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;

namespace Seguranca.Swagger;

public static class SwaggerSeguranca
{
    public static IServiceCollection AdicionarSwaggerComSeguranca(this IServiceCollection services)
    {
        services.AddSwaggerGen(opcoes =>
        {
            opcoes.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Informe o JWT obtido no endpoint de autenticação DEV."
            });

            opcoes.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                [new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                }] = Array.Empty<string>()
            });
        });

        return services;
    }
}
