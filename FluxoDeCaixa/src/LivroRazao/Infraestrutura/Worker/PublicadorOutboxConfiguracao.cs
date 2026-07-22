namespace LivroRazao.Infraestrutura.Worker;

public sealed class PublicadorOutboxConfiguracao
{
    public int IntervaloEmSegundos { get; init; } = 5;
    public int LimiteDeTentativas { get; init; } = 5;
    public int TempoLimiteDeProcessamentoEmMinutos { get; init; } = 5;

    public const string Secao = "PublicadorOutbox";
}
