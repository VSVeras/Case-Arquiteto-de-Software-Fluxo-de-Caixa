using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using Xunit;

namespace ConsolidadoDiario.TesteDeIntegracao.Api;

public sealed class GeradorDeTokenJwtTestes
{
    [Fact]
    public void GerarDeveCriarJwtComConfiguracaoEsperada()
    {
        var token = new JwtSecurityTokenHandler().ReadJwtToken(GeradorDeTokenJwt.Gerar());
        token.Issuer.Should().Be("FluxoDeCaixa");
        token.Audiences.Should().ContainSingle().Which.Should().Be("FluxoDeCaixa.Api");
        token.Header.Alg.Should().Be("HS256");
        token.Claims.Should().Contain(x => x.Type == ClaimTypes.NameIdentifier && x.Value == "usuario-teste");
        token.ValidTo.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public void GerarComIssuerInvalidoDeveAlterarIssuer() =>
        new JwtSecurityTokenHandler().ReadJwtToken(GeradorDeTokenJwt.GerarComIssuerInvalido()).Issuer
            .Should().NotBe("FluxoDeCaixa");

    [Fact]
    public void GerarComAudienceInvalidaDeveAlterarAudience() =>
        new JwtSecurityTokenHandler().ReadJwtToken(GeradorDeTokenJwt.GerarComAudienceInvalida()).Audiences
            .Should().NotContain("FluxoDeCaixa.Api");

    [Fact]
    public void GerarSemIdentificacaoDeveNaoConterNameIdentifier() =>
        new JwtSecurityTokenHandler().ReadJwtToken(GeradorDeTokenJwt.GerarSemIdentificacaoDoUsuario()).Claims
            .Should().NotContain(x => x.Type == ClaimTypes.NameIdentifier);

    [Fact]
    public void GerarComAlgoritmoHs384DeveUsarHs384() =>
        new JwtSecurityTokenHandler().ReadJwtToken(GeradorDeTokenJwt.GerarComAlgoritmoHs384()).Header.Alg
            .Should().Be("HS384");

    [Fact]
    public void GerarSemAssinaturaDeveUsarAlgoritmoNone() =>
        new JwtSecurityTokenHandler().ReadJwtToken(GeradorDeTokenJwt.GerarSemAssinatura()).Header.Alg
            .Should().Be("none");

    [Fact]
    public void GerarExpiradoDeveCriarTokenExpirado() =>
        new JwtSecurityTokenHandler().ReadJwtToken(GeradorDeTokenJwt.GerarExpirado()).ValidTo
            .Should().BeBefore(DateTime.UtcNow);

    [Fact]
    public void GerarAindaNaoValidoDeveCriarNotBeforeFuturo() =>
        new JwtSecurityTokenHandler().ReadJwtToken(GeradorDeTokenJwt.GerarAindaNaoValido()).ValidFrom
            .Should().BeAfter(DateTime.UtcNow);

    [Fact]
    public void GerarExpiradoRecentementeDeveCriarTokenExpirado() =>
        new JwtSecurityTokenHandler().ReadJwtToken(GeradorDeTokenJwt.GerarExpiradoRecentemente()).ValidTo
            .Should().BeBefore(DateTime.UtcNow);

    [Fact]
    public void GerarSemExpiracaoDeveNaoConterClaimExp() =>
        new JwtSecurityTokenHandler().ReadJwtToken(GeradorDeTokenJwt.GerarSemExpiracao()).Claims
            .Should().NotContain(x => x.Type == JwtRegisteredClaimNames.Exp);

    [Fact]
    public void GerarComIdentificacaoVaziaDeveConterClaimVazio() =>
        new JwtSecurityTokenHandler().ReadJwtToken(GeradorDeTokenJwt.GerarComIdentificacaoDoUsuarioVazia()).Claims
            .Should().Contain(x => x.Type == ClaimTypes.NameIdentifier && x.Value == string.Empty);

    [Fact]
    public void GerarComIdentificacaoEmBrancoDeveConterClaimEmBranco() =>
        new JwtSecurityTokenHandler().ReadJwtToken(GeradorDeTokenJwt.GerarComIdentificacaoDoUsuarioEmBranco()).Claims
            .Should().Contain(x => x.Type == ClaimTypes.NameIdentifier && string.IsNullOrWhiteSpace(x.Value));

    [Fact]
    public void GerarComAssinaturaInvalidaDeveContinuarEstruturalmenteValido()
    {
        var token = new JwtSecurityTokenHandler().ReadJwtToken(GeradorDeTokenJwt.GerarComAssinaturaInvalida());
        token.Issuer.Should().Be("FluxoDeCaixa");
        token.Header.Alg.Should().Be("HS256");
    }
}
