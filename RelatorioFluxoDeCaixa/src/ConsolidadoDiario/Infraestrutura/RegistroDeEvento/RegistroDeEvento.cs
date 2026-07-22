using ConsolidadoDiario.Aplicacao.Abstracao;
using Microsoft.Extensions.Logging;
using System;

namespace ConsolidadoDiario.Infraestrutura.RegistroDeEvento;

public sealed class RegistroDeEvento(ILogger<RegistroDeEvento> logger)
    : IRegistroDeEvento
{
    public void Informacao(string mensagem, params object[] argumentos)
    {
        logger.LogInformation(mensagem, argumentos);
    }

    public void Aviso(Exception? excecao, string mensagem, params object[] argumentos)
    {
        logger.LogWarning(excecao, mensagem, argumentos);
    }

    public void Erro(Exception? excecao, string mensagem, params object[] argumentos)
    {
        logger.LogError(excecao, mensagem, argumentos);
    }
}
