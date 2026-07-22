namespace LivroRazao.Infraestrutura.Persistencia.Entidades.Outbox;

public sealed class LancamentoOutbox
{
    public long Id { get; private set; }
    public Guid CorrelationId { get; private set; }
    public string TipoEvento { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public DateTimeOffset DataOcorrencia { get; private set; }
    public DateTimeOffset DataCriacao { get; private set; }
    public DateTimeOffset? DataPublicacao { get; private set; }
    public DateTimeOffset? DataUltimaTentativa { get; private set; }
    public int Tentativas { get; private set; }
    public string? UltimoErro { get; private set; }
    public StatusDaOutbox Status { get; private set; }

    private const int TamanhoMaximoDoErro = 2000;

    private LancamentoOutbox() { }

    public LancamentoOutbox(
        Guid correlationId,
        string tipoEvento,
        string payload,
        DateTimeOffset dataOcorrencia)
    {
        CorrelationId = correlationId;
        TipoEvento = tipoEvento;
        Payload = payload;
        DataOcorrencia = dataOcorrencia;
        DataCriacao = DateTimeOffset.UtcNow;
        Status = StatusDaOutbox.Pendente;
    }

    public void ReservarParaProcessamento()
    {
        if (Status != StatusDaOutbox.Pendente && Status != StatusDaOutbox.Processando)
        {
            throw new InvalidOperationException("O evento não pode ser reservado para processamento.");
        }

        Status = StatusDaOutbox.Processando;
        DataUltimaTentativa = DateTimeOffset.UtcNow;
    }

    public void MarcarComoPublicado()
    {
        if (Status != StatusDaOutbox.Processando)
        {
            throw new InvalidOperationException("Somente eventos em processamento podem ser publicados.");
        }

        Status = StatusDaOutbox.Publicado;
        DataPublicacao = DateTimeOffset.UtcNow;
        UltimoErro = null;
    }

    public void RegistrarFalha(string erro, int limiteDeTentativas)
    {
        if (Status != StatusDaOutbox.Processando)
        {
            throw new InvalidOperationException("Somente eventos em processamento podem registrar falha.");
        }

        if (limiteDeTentativas <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(limiteDeTentativas),
                "O limite de tentativas deve ser maior que zero."
                );
        }

        Tentativas++;
        DataUltimaTentativa = DateTimeOffset.UtcNow;
        UltimoErro = ObterErroLimitado(erro);
        Status = Tentativas >= limiteDeTentativas ? StatusDaOutbox.Erro : StatusDaOutbox.Pendente;
    }

    private static string ObterErroLimitado(string erro)
    {
        if (string.IsNullOrWhiteSpace(erro))
        {
            return "Erro não informado.";
        }

        return erro.Length <= TamanhoMaximoDoErro ? erro : erro[..TamanhoMaximoDoErro];
    }
}
