using System;
using System.Linq;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Seguranca.Autenticacao;
using Seguranca.Autorizacao;

namespace Seguranca;

public static class InjecaoDeDependencia
{
    public static IServiceCollection AdicionarSeguranca(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtConfiguracao = configuration
            .GetSection(JwtConfiguracao.Secao)
            .Get<JwtConfiguracao>()
            ?? throw new InvalidOperationException("A configuração JWT não foi encontrada.");

        ValidarConfiguracao(jwtConfiguracao);

        services.Configure<JwtConfiguracao>(configuration.GetSection(JwtConfiguracao.Secao));

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(opcoes =>
            {
                opcoes.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtConfiguracao.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtConfiguracao.Audience,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    ClockSkew = TimeSpan.Zero,
                    RequireSignedTokens = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtConfiguracao.Chave)),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256]
                };
            });

        services.AddAuthorization(opcoes =>
        {
            opcoes.AddPolicy(PoliticasDeAutorizacao.UsuarioIdentificado, politica =>
            {
                politica.RequireAuthenticatedUser();
                politica.RequireAssertion(contexto =>
                    contexto.User.Claims.Any(claim =>
                        claim.Type == ClaimTypes.NameIdentifier &&
                        !string.IsNullOrWhiteSpace(claim.Value)));
            });

            opcoes.AddPolicy(PoliticasDeAutorizacao.Lancamento, politica =>
            {
                politica.RequireAuthenticatedUser();
                politica.RequireClaim(TiposDeClaim.Autorizacao, AutorizacaoFuncional.Lancamento.ToString());
            });

            opcoes.AddPolicy(PoliticasDeAutorizacao.ConsolidadoDiario, politica =>
            {
                politica.RequireAuthenticatedUser();
                politica.RequireClaim(TiposDeClaim.Autorizacao, AutorizacaoFuncional.ConsolidadoDiario.ToString());
            });
        });

        return services;
    }

    public static IServiceCollection AdicionarAutenticacaoDesenvolvimento(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<AutenticacaoDesenvolvimentoConfiguracao>(
            configuration.GetSection(AutenticacaoDesenvolvimentoConfiguracao.Secao));
        services.AddSingleton<IAutenticacaoDesenvolvimento, AutenticacaoDesenvolvimento>();
        return services;
    }

    private static void ValidarConfiguracao(JwtConfiguracao jwtConfiguracao)
    {
        if (string.IsNullOrWhiteSpace(jwtConfiguracao.Issuer))
            throw new InvalidOperationException("O Issuer JWT não foi configurado.");
        if (string.IsNullOrWhiteSpace(jwtConfiguracao.Audience))
            throw new InvalidOperationException("O Audience JWT não foi configurado.");
        if (string.IsNullOrWhiteSpace(jwtConfiguracao.Chave))
            throw new InvalidOperationException("A chave JWT não foi configurada.");
        if (Encoding.UTF8.GetByteCount(jwtConfiguracao.Chave) < 32)
            throw new InvalidOperationException("A chave JWT deve possuir pelo menos 256 bits.");
    }
}
