namespace LivroRazao.Infraestrutura.Worker;

public sealed class PublicadorOutboxConfiguracao
{
    public const string Secao = "PublicadorOutbox";

    public int IntervaloEmSegundos { get; init; } = 5;

    // A cada N falhas consecutivas, eleva o log de Warning para Error.
    // Nao limita nem interrompe novas tentativas de publicacao.
    public int IntervaloDeAlertaDeTentativas { get; init; } = 5;

    public int QuantidadePorLote { get; init; } = 100;
    public int TempoLimiteDeProcessamentoEmMinutos { get; init; } = 5;
}
