using LivroRazao.Aplicacao.Abstracao;

namespace LivroRazao.Infraestrutura.RegistroDeEvento;

public sealed class RegistroDeEvento(ILogger<RegistroDeEvento> logger) : IRegistroDeEvento
{
    public void Informacao(string mensagem, params object?[] argumentos)
    {
        logger.LogInformation(mensagem, argumentos);
    }

    public void Aviso(Exception? excecao, string mensagem, params object?[] argumentos)
    {
        logger.LogWarning(excecao, mensagem, argumentos);
    }

    public void Erro(Exception? excecao, string mensagem, params object?[] argumentos)
    {
        logger.LogError(excecao, mensagem, argumentos);
    }
}
