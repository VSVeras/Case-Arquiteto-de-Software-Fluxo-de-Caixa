namespace LivroRazao.Dominio;

public sealed class ExcecaoDeDominio : Exception
{
    public IReadOnlyCollection<string> Erros { get; }

    public ExcecaoDeDominio(IEnumerable<string> erros) : this(erros.ToArray())
    {
    }

    private ExcecaoDeDominio(string[] erros) : base(string.Join(" | ", erros))
    {
        Erros = Array.AsReadOnly(erros);
    }
}
