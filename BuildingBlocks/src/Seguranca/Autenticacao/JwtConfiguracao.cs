namespace Seguranca.Autenticacao;

public sealed class JwtConfiguracao
{
    public const string Secao = "Jwt";

    public string Issuer { get; init; } = string.Empty;

    public string Audience { get; init; } = string.Empty;

    public string Chave { get; init; } = string.Empty;
}
