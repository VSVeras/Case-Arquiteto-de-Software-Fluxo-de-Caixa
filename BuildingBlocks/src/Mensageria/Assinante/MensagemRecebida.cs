namespace Mensageria.Assinante;
public sealed record MensagemRecebida(Guid? CorrelationId, string? Tipo, string Conteudo, IReadOnlyDictionary<string, object?> Cabecalhos);
