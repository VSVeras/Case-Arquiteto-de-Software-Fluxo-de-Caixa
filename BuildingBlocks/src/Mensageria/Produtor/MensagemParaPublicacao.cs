namespace Mensageria.Produtor;
public sealed record MensagemParaPublicacao(Guid CorrelationId, string Tipo, string Conteudo)
{
    public void Validar()
    {
        if (CorrelationId == Guid.Empty) throw new ArgumentException("O correlationId deve ser informado.", nameof(CorrelationId));
        if (string.IsNullOrWhiteSpace(Tipo)) throw new ArgumentException("O tipo da mensagem deve ser informado.", nameof(Tipo));
        if (string.IsNullOrWhiteSpace(Conteudo)) throw new ArgumentException("O conteudo da mensagem deve ser informado.", nameof(Conteudo));
    }
}
