using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace ConsolidadoDiario.TesteDeIntegracao.Api;

internal static class GeradorDeTokenJwt
{
    private const string Issuer = "FluxoDeCaixa";
    private const string Audience = "FluxoDeCaixa.Api";
    private const string Chave = "FluxoDeCaixa-Desenvolvimento-Chave-JWT-2026-Segura";

    public static string Gerar()
    {
        var chave =
            new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(Chave));

        var credenciais =
            new SigningCredentials(
                chave,
                SecurityAlgorithms.HmacSha256);

        var token =
            new JwtSecurityToken(
                issuer: Issuer,
                audience: Audience,
                claims:
                [
                    new Claim(
                        ClaimTypes.NameIdentifier,
                        "usuario-teste"),
                    new Claim("autorizacao", "CONTABILIDADE/RELATORIO FLUXO DE CAIXA/CONSOLIDADO DIARIO")
                ],
                notBefore: DateTime.UtcNow,
                expires: DateTime.UtcNow.AddMinutes(30),
                signingCredentials: credenciais);

        return new JwtSecurityTokenHandler()
            .WriteToken(token);
    }

    public static string GerarComAssinaturaInvalida()
    {
        const string chaveInvalida =
            "FluxoDeCaixa-Testes-Chave-JWT-Incorreta-2026-Integracao";

        var chave =
            new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(chaveInvalida));

        var credenciais =
            new SigningCredentials(
                chave,
                SecurityAlgorithms.HmacSha256);

        var token =
            new JwtSecurityToken(
                issuer: Issuer,
                audience: Audience,
                claims:
                [
                    new Claim(
                    ClaimTypes.NameIdentifier,
                    "usuario-teste"),
                    new Claim("autorizacao", "CONTABILIDADE/RELATORIO FLUXO DE CAIXA/CONSOLIDADO DIARIO")
                ],
                notBefore: DateTime.UtcNow,
                expires: DateTime.UtcNow.AddMinutes(30),
                signingCredentials: credenciais);

        return new JwtSecurityTokenHandler()
            .WriteToken(token);
    }

    public static string GerarExpirado()
    {
        var chave =
            new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(Chave));

        var credenciais =
            new SigningCredentials(
                chave,
                SecurityAlgorithms.HmacSha256);

        var token =
            new JwtSecurityToken(
                issuer: Issuer,
                audience: Audience,
                claims:
                [
                    new Claim(
                    ClaimTypes.NameIdentifier,
                    "usuario-teste"),
                    new Claim("autorizacao", "CONTABILIDADE/RELATORIO FLUXO DE CAIXA/CONSOLIDADO DIARIO")
                ],
                notBefore: DateTime.UtcNow.AddHours(-2),
                expires: DateTime.UtcNow.AddHours(-1),
                signingCredentials: credenciais);

        return new JwtSecurityTokenHandler()
            .WriteToken(token);
    }

    public static string GerarComIssuerInvalido()
    {
        const string issuerInvalido = "FluxoDeCaixa.Issuer.Invalido";

        var chave =
            new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(Chave));

        var credenciais =
            new SigningCredentials(
                chave,
                SecurityAlgorithms.HmacSha256);

        var token =
            new JwtSecurityToken(
                issuer: issuerInvalido,
                audience: Audience,
                claims:
                [
                    new Claim(
                    ClaimTypes.NameIdentifier,
                    "usuario-teste"),
                    new Claim("autorizacao", "CONTABILIDADE/RELATORIO FLUXO DE CAIXA/CONSOLIDADO DIARIO")
                ],
                notBefore: DateTime.UtcNow,
                expires: DateTime.UtcNow.AddMinutes(30),
                signingCredentials: credenciais);

        return new JwtSecurityTokenHandler()
            .WriteToken(token);
    }

    public static string GerarComAudienceInvalida()
    {
        const string audienceInvalida = "FluxoDeCaixa.Api.Invalida";

        var chave =
            new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(Chave));

        var credenciais =
            new SigningCredentials(
                chave,
                SecurityAlgorithms.HmacSha256);

        var token =
            new JwtSecurityToken(
                issuer: Issuer,
                audience: audienceInvalida,
                claims:
                [
                    new Claim(
                    ClaimTypes.NameIdentifier,
                    "usuario-teste"),
                    new Claim("autorizacao", "CONTABILIDADE/RELATORIO FLUXO DE CAIXA/CONSOLIDADO DIARIO")
                ],
                notBefore: DateTime.UtcNow,
                expires: DateTime.UtcNow.AddMinutes(30),
                signingCredentials: credenciais);

        return new JwtSecurityTokenHandler()
            .WriteToken(token);
    }

    public static string GerarAindaNaoValido()
    {
        var chave =
            new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(Chave));

        var credenciais =
            new SigningCredentials(
                chave,
                SecurityAlgorithms.HmacSha256);

        var token =
            new JwtSecurityToken(
                issuer: Issuer,
                audience: Audience,
                claims:
                [
                    new Claim(
                    ClaimTypes.NameIdentifier,
                    "usuario-teste"),
                    new Claim("autorizacao", "CONTABILIDADE/RELATORIO FLUXO DE CAIXA/CONSOLIDADO DIARIO")
                ],
                notBefore: DateTime.UtcNow.AddHours(1),
                expires: DateTime.UtcNow.AddHours(2),
                signingCredentials: credenciais);

        return new JwtSecurityTokenHandler()
            .WriteToken(token);
    }

    public static string GerarExpiradoRecentemente()
    {
        var chave =
            new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(Chave));

        var credenciais =
            new SigningCredentials(
                chave,
                SecurityAlgorithms.HmacSha256);

        var agora = DateTime.UtcNow;

        var token =
            new JwtSecurityToken(
                issuer: Issuer,
                audience: Audience,
                claims:
                [
                    new Claim(
                    ClaimTypes.NameIdentifier,
                    "usuario-teste"),
                    new Claim("autorizacao", "CONTABILIDADE/RELATORIO FLUXO DE CAIXA/CONSOLIDADO DIARIO")
                ],
                notBefore: agora.AddMinutes(-5),
                expires: agora.AddSeconds(-1),
                signingCredentials: credenciais);

        return new JwtSecurityTokenHandler()
            .WriteToken(token);
    }

    public static string GerarComAlgoritmoHs384()
    {
        var chave =
            new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(Chave));

        var credenciais =
            new SigningCredentials(
                chave,
                SecurityAlgorithms.HmacSha384);

        var token =
            new JwtSecurityToken(
                issuer: Issuer,
                audience: Audience,
                claims:
                [
                    new Claim(
                    ClaimTypes.NameIdentifier,
                    "usuario-teste"),
                    new Claim("autorizacao", "CONTABILIDADE/RELATORIO FLUXO DE CAIXA/CONSOLIDADO DIARIO")
                ],
                notBefore: DateTime.UtcNow,
                expires: DateTime.UtcNow.AddMinutes(30),
                signingCredentials: credenciais);

        return new JwtSecurityTokenHandler()
            .WriteToken(token);
    }

    public static string GerarSemAssinatura()
    {
        var token =
            new JwtSecurityToken(
                issuer: Issuer,
                audience: Audience,
                claims:
                [
                    new Claim(
                    ClaimTypes.NameIdentifier,
                    "usuario-teste"),
                    new Claim("autorizacao", "CONTABILIDADE/RELATORIO FLUXO DE CAIXA/CONSOLIDADO DIARIO")
                ],
                notBefore: DateTime.UtcNow,
                expires: DateTime.UtcNow.AddMinutes(30),
                signingCredentials: null);

        return new JwtSecurityTokenHandler()
            .WriteToken(token);
    }

    public static string GerarSemExpiracao()
    {
        var chave =
            new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(Chave));

        var credenciais =
            new SigningCredentials(
                chave,
                SecurityAlgorithms.HmacSha256);

        var token =
            new JwtSecurityToken(
                issuer: Issuer,
                audience: Audience,
                claims:
                [
                    new Claim(
                    ClaimTypes.NameIdentifier,
                    "usuario-teste"),
                    new Claim("autorizacao", "CONTABILIDADE/RELATORIO FLUXO DE CAIXA/CONSOLIDADO DIARIO")
                ],
                notBefore: DateTime.UtcNow,
                expires: null,
                signingCredentials: credenciais);

        return new JwtSecurityTokenHandler()
            .WriteToken(token);
    }

    public static string GerarSemIdentificacaoDoUsuario()
    {
        var chave =
            new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(Chave));

        var credenciais =
            new SigningCredentials(
                chave,
                SecurityAlgorithms.HmacSha256);

        var token =
            new JwtSecurityToken(
                issuer: Issuer,
                audience: Audience,
                claims: [],
                notBefore: DateTime.UtcNow,
                expires: DateTime.UtcNow.AddMinutes(30),
                signingCredentials: credenciais);

        return new JwtSecurityTokenHandler()
            .WriteToken(token);
    }

    public static string GerarComIdentificacaoDoUsuarioVazia()
    {
        var chave =
            new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(Chave));

        var credenciais =
            new SigningCredentials(
                chave,
                SecurityAlgorithms.HmacSha256);

        var token =
            new JwtSecurityToken(
                issuer: Issuer,
                audience: Audience,
                claims:
                [
                    new Claim(
                    ClaimTypes.NameIdentifier,
                    string.Empty)
                ],
                notBefore: DateTime.UtcNow,
                expires: DateTime.UtcNow.AddMinutes(30),
                signingCredentials: credenciais);

        return new JwtSecurityTokenHandler()
            .WriteToken(token);
    }

    public static string GerarComIdentificacaoDoUsuarioEmBranco()
    {
        var chave =
            new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(Chave));

        var credenciais =
            new SigningCredentials(
                chave,
                SecurityAlgorithms.HmacSha256);

        var token =
            new JwtSecurityToken(
                issuer: Issuer,
                audience: Audience,
                claims:
                [
                    new Claim(
                    ClaimTypes.NameIdentifier,
                    "   ")
                ],
                notBefore: DateTime.UtcNow,
                expires: DateTime.UtcNow.AddMinutes(30),
                signingCredentials: credenciais);

        return new JwtSecurityTokenHandler()
            .WriteToken(token);
    }
}
