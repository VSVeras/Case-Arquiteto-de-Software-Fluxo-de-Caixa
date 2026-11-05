namespace LivroRazao.Infraestrutura.Persistencia.Entidade.Outbox;

public sealed class LancamentoOutbox
{
    private const int TamanhoMaximoDoErro = 2000;

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

    private LancamentoOutbox()
    { }

    public Guid CorrelationId { get; private set; }
    public DateTimeOffset DataCriacao { get; private set; }
    public DateTimeOffset DataOcorrencia { get; private set; }
    public DateTimeOffset? DataPublicacao { get; private set; }
    public DateTimeOffset? DataUltimaTentativa { get; private set; }
    public long Id { get; private set; }
    public string Payload { get; private set; } = string.Empty;
    public StatusDaOutbox Status { get; private set; }
    public int Tentativas { get; private set; }
    public string TipoEvento { get; private set; } = string.Empty;
    public string? UltimoErro { get; private set; }

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

    public void RegistrarFalha(string erro)
    {
        if (Status != StatusDaOutbox.Processando)
        {
            throw new InvalidOperationException("Somente eventos em processamento podem registrar falha.");
        }


        Tentativas++;
        DataUltimaTentativa = DateTimeOffset.UtcNow;
        UltimoErro = ObterErroLimitado(erro);
        // Falhas de publicação são tratadas como recuperáveis.
        // O evento permanece elegível para novas tentativas para que uma
        // indisponibilidade prolongada do broker não cause perda operacional.
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

    private static string ObterErroLimitado(string erro)
    {
        if (string.IsNullOrWhiteSpace(erro))
        {
            return "Erro não informado.";
        }

        return erro.Length <= TamanhoMaximoDoErro ? erro : erro[..TamanhoMaximoDoErro];
    }
}
