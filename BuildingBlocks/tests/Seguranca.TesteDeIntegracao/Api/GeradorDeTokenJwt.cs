using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Seguranca.TesteDeIntegracao.Api;

internal static class GeradorDeTokenJwt
{
    public static string Gerar()
    {
        return GerarToken(
            ConfiguracaoDeTeste.Issuer,
            ConfiguracaoDeTeste.Audience,
            ConfiguracaoDeTeste.Chave,
            SecurityAlgorithms.HmacSha256,
            [new Claim(ClaimTypes.NameIdentifier, "usuario-teste")],
            DateTime.UtcNow,
            DateTime.UtcNow.AddMinutes(30));
    }

    public static string GerarComAssinaturaInvalida()
    {
        const string chaveInvalida =
            "BuildingBlocks-Testes-Chave-JWT-Incorreta-2026-Integracao";

        return GerarToken(
            ConfiguracaoDeTeste.Issuer,
            ConfiguracaoDeTeste.Audience,
            chaveInvalida,
            SecurityAlgorithms.HmacSha256,
            [new Claim(ClaimTypes.NameIdentifier, "usuario-teste")],
            DateTime.UtcNow,
            DateTime.UtcNow.AddMinutes(30));
    }

    public static string GerarExpirado()
    {
        return GerarToken(
            ConfiguracaoDeTeste.Issuer,
            ConfiguracaoDeTeste.Audience,
            ConfiguracaoDeTeste.Chave,
            SecurityAlgorithms.HmacSha256,
            [new Claim(ClaimTypes.NameIdentifier, "usuario-teste")],
            DateTime.UtcNow.AddHours(-2),
            DateTime.UtcNow.AddHours(-1));
    }

    public static string GerarComIssuerInvalido()
    {
        return GerarToken(
            "BuildingBlocks.Issuer.Invalido",
            ConfiguracaoDeTeste.Audience,
            ConfiguracaoDeTeste.Chave,
            SecurityAlgorithms.HmacSha256,
            [new Claim(ClaimTypes.NameIdentifier, "usuario-teste")],
            DateTime.UtcNow,
            DateTime.UtcNow.AddMinutes(30));
    }

    public static string GerarComAudienceInvalida()
    {
        return GerarToken(
            ConfiguracaoDeTeste.Issuer,
            "BuildingBlocks.Api.Invalida",
            ConfiguracaoDeTeste.Chave,
            SecurityAlgorithms.HmacSha256,
            [new Claim(ClaimTypes.NameIdentifier, "usuario-teste")],
            DateTime.UtcNow,
            DateTime.UtcNow.AddMinutes(30));
    }

    public static string GerarAindaNaoValido()
    {
        return GerarToken(
            ConfiguracaoDeTeste.Issuer,
            ConfiguracaoDeTeste.Audience,
            ConfiguracaoDeTeste.Chave,
            SecurityAlgorithms.HmacSha256,
            [new Claim(ClaimTypes.NameIdentifier, "usuario-teste")],
            DateTime.UtcNow.AddHours(1),
            DateTime.UtcNow.AddHours(2));
    }

    public static string GerarExpiradoRecentemente()
    {
        var agora = DateTime.UtcNow;

        return GerarToken(
            ConfiguracaoDeTeste.Issuer,
            ConfiguracaoDeTeste.Audience,
            ConfiguracaoDeTeste.Chave,
            SecurityAlgorithms.HmacSha256,
            [new Claim(ClaimTypes.NameIdentifier, "usuario-teste")],
            agora.AddMinutes(-5),
            agora.AddSeconds(-1));
    }

    public static string GerarComAlgoritmoHs384()
    {
        return GerarToken(
            ConfiguracaoDeTeste.Issuer,
            ConfiguracaoDeTeste.Audience,
            ConfiguracaoDeTeste.Chave,
            SecurityAlgorithms.HmacSha384,
            [new Claim(ClaimTypes.NameIdentifier, "usuario-teste")],
            DateTime.UtcNow,
            DateTime.UtcNow.AddMinutes(30));
    }

    public static string GerarSemAssinatura()
    {
        var token = new JwtSecurityToken(
            issuer: ConfiguracaoDeTeste.Issuer,
            audience: ConfiguracaoDeTeste.Audience,
            claims:
            [
                new Claim(
                    ClaimTypes.NameIdentifier,
                    "usuario-teste")
            ],
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: null);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static string GerarSemExpiracao()
    {
        var chave = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(ConfiguracaoDeTeste.Chave));
        var credenciais = new SigningCredentials(
            chave,
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: ConfiguracaoDeTeste.Issuer,
            audience: ConfiguracaoDeTeste.Audience,
            claims:
            [
                new Claim(
                    ClaimTypes.NameIdentifier,
                    "usuario-teste")
            ],
            notBefore: DateTime.UtcNow,
            expires: null,
            signingCredentials: credenciais);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static string GerarSemIdentificacaoDoUsuario()
    {
        return GerarToken(
            ConfiguracaoDeTeste.Issuer,
            ConfiguracaoDeTeste.Audience,
            ConfiguracaoDeTeste.Chave,
            SecurityAlgorithms.HmacSha256,
            [],
            DateTime.UtcNow,
            DateTime.UtcNow.AddMinutes(30));
    }

    public static string GerarComIdentificacaoDoUsuarioVazia()
    {
        return GerarToken(
            ConfiguracaoDeTeste.Issuer,
            ConfiguracaoDeTeste.Audience,
            ConfiguracaoDeTeste.Chave,
            SecurityAlgorithms.HmacSha256,
            [new Claim(ClaimTypes.NameIdentifier, string.Empty)],
            DateTime.UtcNow,
            DateTime.UtcNow.AddMinutes(30));
    }

    public static string GerarComIdentificacaoDoUsuarioEmBranco()
    {
        return GerarToken(
            ConfiguracaoDeTeste.Issuer,
            ConfiguracaoDeTeste.Audience,
            ConfiguracaoDeTeste.Chave,
            SecurityAlgorithms.HmacSha256,
            [new Claim(ClaimTypes.NameIdentifier, "   ")],
            DateTime.UtcNow,
            DateTime.UtcNow.AddMinutes(30));
    }

    private static string GerarToken(
        string issuer,
        string audience,
        string chaveTexto,
        string algoritmo,
        IEnumerable<Claim> claims,
        DateTime notBefore,
        DateTime expires)
    {
        var chave = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(chaveTexto));
        var credenciais = new SigningCredentials(chave, algoritmo);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: notBefore,
            expires: expires,
            signingCredentials: credenciais);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
