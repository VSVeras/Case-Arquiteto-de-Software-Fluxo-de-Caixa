using System.Net;
using System.Net.Http.Headers;
using ConsolidadoDiario.Aplicacao.Abstracao;
using ConsolidadoDiario.Aplicacao.Dto;
using Mensageria.RabbitMQ.Topologia;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace ConsolidadoDiario.TesteDeIntegracao.Api;

public sealed class ConsolidadoDiarioApiTestes
{
    [Fact]
    public async Task GetComTokenValidoDeveRetornar200()
    {
        var servico = new Mock<IConsultaConsolidadoDiarioServico>();
        var data = new DateOnly(2026, 8, 13);
        servico.Setup(x => x.ObterPorDataAsync(data, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConsolidadoDiarioResposta(data, 100, 20, 80, DateTimeOffset.UtcNow));
        using var fabrica = new FabricaDaApi(servico.Object);
        using var cliente = CriarCliente(fabrica, GeradorDeTokenJwt.Gerar());
        var resultado = await cliente.GetAsync($"/api/consolidados-diarios/{data:yyyy-MM-dd}");
        resultado.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetSemTokenDeveRetornar401()
    {
        var servico = new Mock<IConsultaConsolidadoDiarioServico>();
        using var fabrica = new FabricaDaApi(servico.Object);
        using var cliente = fabrica.CreateClient();
        var resultado = await cliente.GetAsync("/api/consolidados-diarios/2026-08-13");
        resultado.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        servico.Verify(x => x.ObterPorDataAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public Task GetComTokenTextoInvalidoDeveRetornar401() => Validar401Async("token-jwt-invalido");

    [Fact]
    public Task GetComAssinaturaInvalidaDeveRetornar401() => Validar401Async(GeradorDeTokenJwt.GerarComAssinaturaInvalida());

    [Fact]
    public Task GetComTokenExpiradoDeveRetornar401() => Validar401Async(GeradorDeTokenJwt.GerarExpirado());

    [Fact]
    public Task GetComIssuerInvalidoDeveRetornar401() => Validar401Async(GeradorDeTokenJwt.GerarComIssuerInvalido());

    [Fact]
    public Task GetComAudienceInvalidaDeveRetornar401() => Validar401Async(GeradorDeTokenJwt.GerarComAudienceInvalida());

    [Fact]
    public Task GetComTokenAindaNaoValidoDeveRetornar401() => Validar401Async(GeradorDeTokenJwt.GerarAindaNaoValido());

    [Fact]
    public Task GetComTokenExpiradoRecentementeDeveRetornar401() => Validar401Async(GeradorDeTokenJwt.GerarExpiradoRecentemente());

    [Fact]
    public Task GetComAlgoritmoNaoPermitidoDeveRetornar401() => Validar401Async(GeradorDeTokenJwt.GerarComAlgoritmoHs384());

    [Fact]
    public Task GetComTokenSemAssinaturaDeveRetornar401() => Validar401Async(GeradorDeTokenJwt.GerarSemAssinatura());

    [Fact]
    public Task GetComTokenSemExpiracaoDeveRetornar401() => Validar401Async(GeradorDeTokenJwt.GerarSemExpiracao());

    [Fact]
    public Task GetSemIdentificacaoDoUsuarioDeveRetornar403() => Validar403Async(GeradorDeTokenJwt.GerarSemIdentificacaoDoUsuario());

    [Fact]
    public Task GetComIdentificacaoVaziaDeveRetornar403() => Validar403Async(GeradorDeTokenJwt.GerarComIdentificacaoDoUsuarioVazia());

    [Fact]
    public Task GetComIdentificacaoEmBrancoDeveRetornar403() => Validar403Async(GeradorDeTokenJwt.GerarComIdentificacaoDoUsuarioEmBranco());

    private static async Task Validar401Async(string token)
    {
        var servico = new Mock<IConsultaConsolidadoDiarioServico>();
        using var fabrica = new FabricaDaApi(servico.Object);
        using var cliente = CriarCliente(fabrica, token);
        var resultado = await cliente.GetAsync("/api/consolidados-diarios/2026-08-13");
        resultado.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static async Task Validar403Async(string token)
    {
        var servico = new Mock<IConsultaConsolidadoDiarioServico>();
        using var fabrica = new FabricaDaApi(servico.Object);
        using var cliente = CriarCliente(fabrica, token);
        var resultado = await cliente.GetAsync("/api/consolidados-diarios/2026-08-13");
        resultado.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static HttpClient CriarCliente(WebApplicationFactory<Program> fabrica, string token)
    {
        var cliente = fabrica.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return cliente;
    }

    private sealed class FabricaDaApi(
    IConsultaConsolidadoDiarioServico servico)
    : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(
            IWebHostBuilder builder)
        {
            builder.UseSetting(
                "Jwt:Issuer",
                "FluxoDeCaixa");

            builder.UseSetting(
                "Jwt:Audience",
                "FluxoDeCaixa.Api");

            builder.UseSetting(
                "Jwt:Chave",
                "FluxoDeCaixa-Desenvolvimento-Chave-JWT-2026-Segura");

            builder.ConfigureAppConfiguration((_, configuracao) =>
            {
                configuracao.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:ConsolidadoDiario"] =
                            $"Host=localhost;Port=5432;Database=teste;" +
                            $"Username={Guid.NewGuid():N};" +
                            $"Password={Guid.NewGuid():N}",

                        ["RabbitMq:Host"] = "localhost",
                        ["RabbitMq:Porta"] = "5672",
                        ["RabbitMq:Usuario"] = "guest",
                        ["RabbitMq:Senha"] = "guest",
                        ["RabbitMq:VirtualHost"] = "/",
                        ["RabbitMq:Exchange"] = "teste",
                        ["RabbitMq:RoutingKey"] = "teste",
                        ["RabbitMq:Fila"] = "teste",
                        ["RabbitMq:QuantidadeDeMensagensEmProcessamento"] = "1",

                        ["Jwt:Issuer"] = "FluxoDeCaixa",
                        ["Jwt:Audience"] = "FluxoDeCaixa.Api",
                        ["Jwt:Chave"] =
                            "FluxoDeCaixa-Desenvolvimento-Chave-JWT-2026-Segura"
                    });
            });

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IConsultaConsolidadoDiarioServico>();
                services.AddSingleton(servico);

                services.RemoveAll<IInicializadorRabbitMq>();
                services.AddSingleton(
                    Mock.Of<IInicializadorRabbitMq>());

                foreach (var descriptor in services
                    .Where(x =>
                        x.ServiceType == typeof(IHostedService))
                    .ToArray())
                {
                    services.Remove(descriptor);
                }
            });
        }
    }
}
