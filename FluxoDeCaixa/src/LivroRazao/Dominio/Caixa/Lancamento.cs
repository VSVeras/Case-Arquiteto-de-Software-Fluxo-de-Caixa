namespace LivroRazao.Dominio.Caixa;

public sealed class Lancamento
{
    public long Id { get; private set; }
    public Guid CorrelationId { get; private set; }
    public TipoDeLancamento Tipo { get; private set; }
    public DateTimeOffset DataLancamento { get; private set; }
    public decimal Valor { get; private set; }
    public string? Descricao { get; private set; }
    public DateTimeOffset DataCriacao { get; private set; }

    private Lancamento()
    {
    }

    public Lancamento(
        Guid correlationId,
        TipoDeLancamento tipo,
        DateTimeOffset dataLancamento,
        decimal valor,
        string? descricao)
    {
        CorrelationId = correlationId;
        Tipo = tipo;
        DataLancamento = dataLancamento;
        Valor = valor;
        Descricao = NormalizarDescricao(descricao);
        DataCriacao = DateTimeOffset.UtcNow;

        Validar();
    }

    public bool RepresentaMesmaOperacao(
        TipoDeLancamento tipo,
        DateTimeOffset dataLancamento,
        decimal valor,
        string? descricao)
    {
        return
            Tipo == tipo
            && DataLancamento == dataLancamento
            && Valor == valor
            && Descricao == NormalizarDescricao(descricao);
    }

    public static string? NormalizarDescricao(string? descricao)
    {
        return string.IsNullOrWhiteSpace(descricao) ? null : descricao.Trim();
    }

    private void Validar()
    {
        var erros = new List<string>();

        if (CorrelationId == Guid.Empty)
        {
            erros.Add("O correlationId deve ser informado.");
        }

        if (!Enum.IsDefined(Tipo))
        {
            erros.Add("O tipo de lançamento é inválido.");
        }

        if (DataLancamento == default)
        {
            erros.Add("A data do lançamento deve ser informada.");
        }

        if (Valor <= 0)
        {
            erros.Add("O valor deve ser maior que zero.");
        }

        if (Descricao?.Length > 100)
        {
            erros.Add("A descrição deve possuir no máximo 100 caracteres.");
        }

        if (erros.Count > 0)
        {
            throw new ExcecaoDeDominio(erros);
        }
    }
}
