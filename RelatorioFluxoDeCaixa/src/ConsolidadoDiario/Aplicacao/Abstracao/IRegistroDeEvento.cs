using System;

namespace ConsolidadoDiario.Aplicacao.Abstracao;

public interface IRegistroDeEvento
{
    void Informacao(string mensagem, params object?[] argumentos);
    void Aviso(Exception? excecao, string mensagem, params object?[] argumentos);
    void Erro(Exception? excecao, string mensagem, params object?[] argumentos);
}
