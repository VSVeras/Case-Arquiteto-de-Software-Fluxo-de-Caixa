using ConsolidadoDiario.Aplicacao.EventosDeIntegracao;

namespace ConsolidadoDiario.Aplicacao.Abstracao;

public interface IProcessadorEventoLancamento
{
    Task<bool> ProcessarAsync(EventoDeLancamentoCriado evento, CancellationToken cancellationToken);
}
