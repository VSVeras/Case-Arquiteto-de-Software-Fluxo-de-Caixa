namespace LivroRazao.Infraestrutura.Excecao;

public sealed class ConflitoDePersistenciaException : Exception
{
    public ConflitoDePersistenciaException(string mensagem)
        : base(mensagem)
    {
    }

    public ConflitoDePersistenciaException(
        string mensagem,
        Exception excecaoInterna
        )
        : base(mensagem, excecaoInterna)
    {
    }
}
