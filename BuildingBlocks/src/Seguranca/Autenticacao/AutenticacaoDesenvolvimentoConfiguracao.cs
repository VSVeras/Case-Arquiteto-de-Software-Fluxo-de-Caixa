namespace Seguranca.Autenticacao;

public sealed class AutenticacaoDesenvolvimentoConfiguracao
{
    public const string Secao = "AutenticacaoDesenvolvimento";

    public string Usuario { get; init; } = string.Empty;
    public string Senha { get; init; } = string.Empty;
}
