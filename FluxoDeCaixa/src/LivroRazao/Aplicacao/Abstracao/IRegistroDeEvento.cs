namespace LivroRazao.Aplicacao.Abstracao;

public interface IRegistroDeEvento
{
    void Aviso(Exception? excecao, string mensagem, params object?[] argumentos);

    void Erro(Exception? excecao, string mensagem, params object?[] argumentos);

    void Informacao(string mensagem, params object?[] argumentos);
}
