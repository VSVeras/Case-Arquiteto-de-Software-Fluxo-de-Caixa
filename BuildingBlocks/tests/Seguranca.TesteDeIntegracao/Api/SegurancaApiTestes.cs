using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Xunit;

namespace Seguranca.TesteDeIntegracao.Api;

public sealed class SegurancaApiTestes
{
    [Fact]
    public async Task EndpointPublicoDeveRetornar200()
    {
        await using var aplicacao = await AplicacaoDeTeste.CriarAsync();
        using var cliente = aplicacao.CriarCliente();

        var resultado = await cliente.GetAsync("/publico");

        resultado.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetComTokenValidoDeveRetornar200()
    {
        await using var aplicacao = await AplicacaoDeTeste.CriarAsync();
        using var cliente = CriarClienteAutenticado(aplicacao);

        var resultado = await cliente.GetAsync("/protegido");

        resultado.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PostComTokenValidoDeveRetornar200()
    {
        await using var aplicacao = await AplicacaoDeTeste.CriarAsync();
        using var cliente = CriarClienteAutenticado(aplicacao);

        var resultado = await cliente.PostAsync(
            "/protegido",
            content: null);

        resultado.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetSemTokenDeveRetornar401()
    {
        await using var aplicacao = await AplicacaoDeTeste.CriarAsync();
        using var cliente = aplicacao.CriarCliente();

        var resultado = await cliente.GetAsync("/protegido");

        resultado.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostSemTokenDeveRetornar401()
    {
        await using var aplicacao = await AplicacaoDeTeste.CriarAsync();
        using var cliente = aplicacao.CriarCliente();

        var resultado = await cliente.PostAsync(
            "/protegido",
            content: null);

        resultado.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetComTokenMalformadoDeveRetornar401()
    {
        await using var aplicacao = await AplicacaoDeTeste.CriarAsync();
        using var cliente = CriarClienteComToken(
            aplicacao,
            "token-jwt-invalido");

        var resultado = await cliente.GetAsync("/protegido");

        resultado.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostComTokenMalformadoDeveRetornar401()
    {
        await using var aplicacao = await AplicacaoDeTeste.CriarAsync();
        using var cliente = CriarClienteComToken(
            aplicacao,
            "token-jwt-invalido");

        var resultado = await cliente.PostAsync(
            "/protegido",
            content: null);

        resultado.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetComAssinaturaJwtInvalidaDeveRetornar401()
    {
        await ValidarUnauthorizedAsync(
            GeradorDeTokenJwt.GerarComAssinaturaInvalida());
    }

    [Fact]
    public async Task GetComTokenExpiradoDeveRetornar401()
    {
        await ValidarUnauthorizedAsync(
            GeradorDeTokenJwt.GerarExpirado());
    }

    [Fact]
    public async Task GetComIssuerJwtInvalidoDeveRetornar401()
    {
        await ValidarUnauthorizedAsync(
            GeradorDeTokenJwt.GerarComIssuerInvalido());
    }

    [Fact]
    public async Task GetComAudienceJwtInvalidaDeveRetornar401()
    {
        await ValidarUnauthorizedAsync(
            GeradorDeTokenJwt.GerarComAudienceInvalida());
    }

    [Fact]
    public async Task GetComTokenAindaNaoValidoDeveRetornar401()
    {
        await ValidarUnauthorizedAsync(
            GeradorDeTokenJwt.GerarAindaNaoValido());
    }

    [Fact]
    public async Task GetComTokenExpiradoRecentementeDeveRetornar401()
    {
        await ValidarUnauthorizedAsync(
            GeradorDeTokenJwt.GerarExpiradoRecentemente());
    }

    [Fact]
    public async Task GetComAlgoritmoJwtNaoPermitidoDeveRetornar401()
    {
        await ValidarUnauthorizedAsync(
            GeradorDeTokenJwt.GerarComAlgoritmoHs384());
    }

    [Fact]
    public async Task GetComTokenJwtSemAssinaturaDeveRetornar401()
    {
        await ValidarUnauthorizedAsync(
            GeradorDeTokenJwt.GerarSemAssinatura());
    }

    [Fact]
    public async Task GetComTokenJwtSemExpiracaoDeveRetornar401()
    {
        await ValidarUnauthorizedAsync(
            GeradorDeTokenJwt.GerarSemExpiracao());
    }

    [Fact]
    public async Task GetComJwtSemIdentificacaoDoUsuarioDeveRetornar403()
    {
        await ValidarForbiddenAsync(
            GeradorDeTokenJwt.GerarSemIdentificacaoDoUsuario());
    }

    [Fact]
    public async Task GetComIdentificacaoDoUsuarioVaziaDeveRetornar403()
    {
        await ValidarForbiddenAsync(
            GeradorDeTokenJwt.GerarComIdentificacaoDoUsuarioVazia());
    }

    [Fact]
    public async Task GetComIdentificacaoDoUsuarioEmBrancoDeveRetornar403()
    {
        await ValidarForbiddenAsync(
            GeradorDeTokenJwt.GerarComIdentificacaoDoUsuarioEmBranco());
    }

    [Fact]
    public async Task PostComJwtSemIdentificacaoDoUsuarioDeveRetornar403()
    {
        await using var aplicacao = await AplicacaoDeTeste.CriarAsync();
        using var cliente = CriarClienteComToken(
            aplicacao,
            GeradorDeTokenJwt.GerarSemIdentificacaoDoUsuario());

        var resultado = await cliente.PostAsync(
            "/protegido",
            content: null);

        resultado.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static async Task ValidarUnauthorizedAsync(string token)
    {
        await using var aplicacao = await AplicacaoDeTeste.CriarAsync();
        using var cliente = CriarClienteComToken(aplicacao, token);

        var resultado = await cliente.GetAsync("/protegido");

        resultado.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static async Task ValidarForbiddenAsync(string token)
    {
        await using var aplicacao = await AplicacaoDeTeste.CriarAsync();
        using var cliente = CriarClienteComToken(aplicacao, token);

        var resultado = await cliente.GetAsync("/protegido");

        resultado.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static HttpClient CriarClienteAutenticado(
        AplicacaoDeTeste aplicacao)
    {
        return CriarClienteComToken(
            aplicacao,
            GeradorDeTokenJwt.Gerar());
    }

    private static HttpClient CriarClienteComToken(
        AplicacaoDeTeste aplicacao,
        string token)
    {
        var cliente = aplicacao.CriarCliente();

        cliente.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                token);

        return cliente;
    }
}
