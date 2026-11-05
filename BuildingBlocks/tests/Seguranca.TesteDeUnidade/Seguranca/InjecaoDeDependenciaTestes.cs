using Seguranca;
using Seguranca.Autorizacao;
using Seguranca.Autenticacao;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Seguranca.TesteDeUnidade.Seguranca;

public sealed class InjecaoDeDependenciaTestes
{
    private const string Issuer = "BuildingBlocks.Testes";
    private const string Audience = "BuildingBlocks.Testes.Api";
    private const string Chave =
        "BuildingBlocks-Testes-Chave-JWT-Segura-2026-Integracao";

    [Fact]
    public async Task DeveRegistrarAutenticacaoJwtComConfiguracaoEsperada()
    {
        var services = new ServiceCollection();
        var configuration = CriarConfiguracaoValida();

        var retorno = services.AdicionarSeguranca(configuration);

        retorno.Should().BeSameAs(services);

        using var provedor = services.BuildServiceProvider();

        var provedorDeEsquemas =
            provedor.GetRequiredService<IAuthenticationSchemeProvider>();

        var esquemaDeAutenticacao =
            await provedorDeEsquemas
                .GetDefaultAuthenticateSchemeAsync();

        var esquemaDeDesafio =
            await provedorDeEsquemas
                .GetDefaultChallengeSchemeAsync();

        esquemaDeAutenticacao.Should().NotBeNull();

        esquemaDeAutenticacao!
            .Name
            .Should()
            .Be(JwtBearerDefaults.AuthenticationScheme);

        esquemaDeDesafio.Should().NotBeNull();

        esquemaDeDesafio!
            .Name
            .Should()
            .Be(JwtBearerDefaults.AuthenticationScheme);

        var opcoesJwt = provedor
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

        var parametros = opcoesJwt.TokenValidationParameters;

        parametros.ValidateIssuer.Should().BeTrue();
        parametros.ValidIssuer.Should().Be(Issuer);

        parametros.ValidateAudience.Should().BeTrue();
        parametros.ValidAudience.Should().Be(Audience);

        parametros.ValidateLifetime.Should().BeTrue();
        parametros.RequireExpirationTime.Should().BeTrue();
        parametros.ClockSkew.Should().Be(TimeSpan.Zero);

        parametros.RequireSignedTokens.Should().BeTrue();

        parametros.ValidateIssuerSigningKey.Should().BeTrue();

        parametros.ValidAlgorithms
            .Should()
            .ContainSingle();

        parametros.ValidAlgorithms
            .Should()
            .Contain(SecurityAlgorithms.HmacSha256);

        var chave = parametros.IssuerSigningKey
            .Should()
            .BeOfType<SymmetricSecurityKey>()
            .Which;

        chave.Key
            .Should()
            .Equal(Encoding.UTF8.GetBytes(Chave));
    }

    [Fact]
    public async Task UsuarioIdentificadoDeveAutorizarUsuarioAutenticadoEIdentificado()
    {
        using var provedor = CriarProvedorComSeguranca();

        var autorizacao =
            provedor.GetRequiredService<IAuthorizationService>();

        var usuario = CriarUsuario(
            new Claim(
                ClaimTypes.NameIdentifier,
                "usuario-teste"));

        var resultado = await autorizacao.AuthorizeAsync(
            usuario,
            resource: null,
            PoliticasDeAutorizacao.UsuarioIdentificado);

        resultado.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task UsuarioIdentificadoDeveRejeitarUsuarioNaoAutenticado()
    {
        using var provedor = CriarProvedorComSeguranca();

        var autorizacao =
            provedor.GetRequiredService<IAuthorizationService>();

        var usuario =
            new ClaimsPrincipal(
                new ClaimsIdentity());

        var resultado = await autorizacao.AuthorizeAsync(
            usuario,
            resource: null,
            PoliticasDeAutorizacao.UsuarioIdentificado);

        resultado.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task UsuarioIdentificadoDeveRejeitarUsuarioSemNameIdentifier()
    {
        using var provedor = CriarProvedorComSeguranca();

        var autorizacao =
            provedor.GetRequiredService<IAuthorizationService>();

        var usuario = CriarUsuario();

        var resultado = await autorizacao.AuthorizeAsync(
            usuario,
            resource: null,
            PoliticasDeAutorizacao.UsuarioIdentificado);

        resultado.Succeeded.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UsuarioIdentificadoDeveRejeitarIdentificacaoSemValor(
        string identificacao)
    {
        using var provedor = CriarProvedorComSeguranca();

        var autorizacao =
            provedor.GetRequiredService<IAuthorizationService>();

        var usuario = CriarUsuario(
            new Claim(
                ClaimTypes.NameIdentifier,
                identificacao));

        var resultado = await autorizacao.AuthorizeAsync(
            usuario,
            resource: null,
            PoliticasDeAutorizacao.UsuarioIdentificado);

        resultado.Succeeded.Should().BeFalse();
    }

    [Fact]
    public void ConfiguracaoJwtAusenteDeveSerRejeitada()
    {
        var services = new ServiceCollection();

        var configuration =
            new ConfigurationBuilder()
                .Build();

        var acao =
            () => services.AdicionarSeguranca(configuration);

        acao.Should()
            .Throw<InvalidOperationException>()
            .WithMessage(
                "*configuração JWT não foi encontrada*");
    }

    [Fact]
    public void IssuerNaoConfiguradoDeveSerRejeitado()
    {
        var configuration = CriarConfiguracao(
            issuer: string.Empty,
            audience: Audience,
            chave: Chave);

        var acao =
            () => new ServiceCollection()
                .AdicionarSeguranca(configuration);

        acao.Should()
            .Throw<InvalidOperationException>()
            .WithMessage(
                "*Issuer JWT não foi configurado*");
    }

    [Fact]
    public void AudienceNaoConfiguradaDeveSerRejeitada()
    {
        var configuration = CriarConfiguracao(
            issuer: Issuer,
            audience: string.Empty,
            chave: Chave);

        var acao =
            () => new ServiceCollection()
                .AdicionarSeguranca(configuration);

        acao.Should()
            .Throw<InvalidOperationException>()
            .WithMessage(
                "*Audience JWT não foi configurado*");
    }

    [Fact]
    public void ChaveNaoConfiguradaDeveSerRejeitada()
    {
        var configuration = CriarConfiguracao(
            issuer: Issuer,
            audience: Audience,
            chave: string.Empty);

        var acao =
            () => new ServiceCollection()
                .AdicionarSeguranca(configuration);

        acao.Should()
            .Throw<InvalidOperationException>()
            .WithMessage(
                "*chave JWT não foi configurada*");
    }

    [Fact]
    public void ChaveMenorQue256BitsDeveSerRejeitada()
    {
        var configuration = CriarConfiguracao(
            issuer: Issuer,
            audience: Audience,
            chave: "chave-curta");

        var acao =
            () => new ServiceCollection()
                .AdicionarSeguranca(configuration);

        acao.Should()
            .Throw<InvalidOperationException>()
            .WithMessage(
                "*pelo menos 256 bits*");
    }

    [Fact]
    public void ChaveCom256BitsEmUtf8DeveSerAceita()
    {
        var configuration = CriarConfiguracao(
            issuer: Issuer,
            audience: Audience,
            chave: "áááááááááááááááá");

        var acao =
            () => new ServiceCollection()
                .AdicionarSeguranca(configuration);

        acao.Should().NotThrow();
    }

    private static ServiceProvider CriarProvedorComSeguranca()
    {
        var services = new ServiceCollection();

        services.AddLogging();

        services.AdicionarSeguranca(
            CriarConfiguracaoValida());

        return services.BuildServiceProvider();
    }

    private static ClaimsPrincipal CriarUsuario(
        params Claim[] claims)
    {
        var identidade = new ClaimsIdentity(
            claims,
            JwtBearerDefaults.AuthenticationScheme);

        return new ClaimsPrincipal(identidade);
    }

    private static IConfiguration CriarConfiguracaoValida()
    {
        return CriarConfiguracao(
            Issuer,
            Audience,
            Chave);
    }

    private static IConfiguration CriarConfiguracao(
        string issuer,
        string audience,
        string chave)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Jwt:Issuer"] = issuer,
                    ["Jwt:Audience"] = audience,
                    ["Jwt:Chave"] = chave
                })
            .Build();
    }
}
