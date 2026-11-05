using Seguranca;
using Seguranca.Autorizacao;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Seguranca.TesteDeIntegracao.Api;

internal sealed class AplicacaoDeTeste : IAsyncDisposable
{
    private readonly WebApplication _app;

    private AplicacaoDeTeste(WebApplication app)
    {
        _app = app;
    }

    public static async Task<AplicacaoDeTeste> CriarAsync()
    {
        var builder = WebApplication.CreateBuilder(
            new WebApplicationOptions
            {
                EnvironmentName = Environments.Development
            });

        builder.WebHost.UseTestServer();

        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = ConfiguracaoDeTeste.Issuer,
                ["Jwt:Audience"] = ConfiguracaoDeTeste.Audience,
                ["Jwt:Chave"] = ConfiguracaoDeTeste.Chave
            });

        builder.Services.AdicionarSeguranca(builder.Configuration);

        var app = builder.Build();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapGet(
            "/publico",
            () => Results.Ok())
            .AllowAnonymous();

        app.MapGet(
            "/protegido",
            () => Results.Ok())
            .RequireAuthorization(
                PoliticasDeAutorizacao.UsuarioIdentificado);

        app.MapPost(
            "/protegido",
            () => Results.Ok())
            .RequireAuthorization(
                PoliticasDeAutorizacao.UsuarioIdentificado);

        await app.StartAsync();

        return new AplicacaoDeTeste(app);
    }

    public HttpClient CriarCliente()
    {
        return _app.GetTestClient();
    }

    public async ValueTask DisposeAsync()
    {
        await _app.DisposeAsync();
    }
}
