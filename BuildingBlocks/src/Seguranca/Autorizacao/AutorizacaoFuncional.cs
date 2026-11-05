namespace Seguranca.Autorizacao;

/// <summary>
/// Objeto de Valor que representa uma autorização no padrão Área/Processo/Operação.
/// </summary>
public sealed record AutorizacaoFuncional
{
    public string Area { get; }
    public string Processo { get; }
    public string Operacao { get; }

    private AutorizacaoFuncional(string area, string processo, string operacao)
    {
        Area = area;
        Processo = processo;
        Operacao = operacao;
    }

    public static readonly AutorizacaoFuncional Lancamento =
        new("CONTABILIDADE", "FLUXO DE CAIXA", "LANCAMENTO");

    public static readonly AutorizacaoFuncional ConsolidadoDiario =
        new("CONTABILIDADE", "RELATORIO FLUXO DE CAIXA", "CONSOLIDADO DIARIO");

    public static IEnumerable<AutorizacaoFuncional> Itens
    {
        get
        {
            yield return Lancamento;
            yield return ConsolidadoDiario;
        }
    }

    public override string ToString() => $"{Area}/{Processo}/{Operacao}";
}
