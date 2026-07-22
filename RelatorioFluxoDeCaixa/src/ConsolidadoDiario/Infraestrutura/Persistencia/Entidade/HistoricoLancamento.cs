namespace ConsolidadoDiario.Infraestrutura.Persistencia.Entidade;

public sealed class HistoricoLancamento
{
    public long Id { get; private set; }
    public Guid CorrelationId { get; private set; }
    public TipoDeLancamento Tipo { get; private set; }
    public DateTimeOffset DataLancamento { get; private set; }
    public decimal Valor { get; private set; }
    public string? Descricao { get; private set; }
    public DateTimeOffset DataProcessamento { get; private set; }

    private HistoricoLancamento()
    {
    }

    public HistoricoLancamento(
        Guid correlationId,
        TipoDeLancamento tipo,
        DateTimeOffset dataLancamento,
        decimal valor,
        string? descricao,
        DateTimeOffset dataProcessamento)
    {
        if (correlationId == Guid.Empty)
        {
            throw new ArgumentException("O correlationId deve ser informado.", nameof(correlationId));
        }

        if (dataLancamento == default)
        {
            throw new ArgumentException("A data do lançamento deve ser informada.", nameof(dataLancamento));
        }

        if (valor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(valor), "O valor deve ser maior que zero.");
        }

        var descricaoNormalizada = string.IsNullOrWhiteSpace(descricao) ? null : descricao.Trim();
        if (descricaoNormalizada?.Length > 100)
        {
            throw new ArgumentException("A descrição deve possuir no máximo 100 caracteres.", nameof(descricao));
        }

        CorrelationId = correlationId;
        Tipo = tipo;
        DataLancamento = dataLancamento;
        Valor = valor;
        Descricao = descricaoNormalizada;
        DataProcessamento = dataProcessamento;
    }
}
