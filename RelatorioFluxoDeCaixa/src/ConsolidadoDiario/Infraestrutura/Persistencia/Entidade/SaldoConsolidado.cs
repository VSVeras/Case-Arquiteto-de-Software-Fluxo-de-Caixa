namespace ConsolidadoDiario.Infraestrutura.Persistencia.Entidade;

public sealed class SaldoConsolidado
{
    public long Id { get; private set; }
    public DateOnly DataReferencia { get; private set; }
    public decimal TotalCreditos { get; private set; }
    public decimal TotalDebitos { get; private set; }
    public decimal SaldoDiarioConsolidado { get; private set; }
    public DateTimeOffset DataAtualizacao { get; private set; }

    private SaldoConsolidado()
    {
    }

    public SaldoConsolidado(
        DateOnly dataReferencia,
        decimal totalCreditos,
        decimal totalDebitos,
        DateTimeOffset dataAtualizacao)
    {
        if (dataReferencia == default)
        {
            throw new ArgumentException("A data de referência deve ser informada.", nameof(dataReferencia));
        }

        if (totalCreditos < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalCreditos));
        }

        if (totalDebitos < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalDebitos));
        }

        DataReferencia = dataReferencia;
        TotalCreditos = totalCreditos;
        TotalDebitos = totalDebitos;
        SaldoDiarioConsolidado = totalCreditos - totalDebitos;
        DataAtualizacao = dataAtualizacao;
    }
}
