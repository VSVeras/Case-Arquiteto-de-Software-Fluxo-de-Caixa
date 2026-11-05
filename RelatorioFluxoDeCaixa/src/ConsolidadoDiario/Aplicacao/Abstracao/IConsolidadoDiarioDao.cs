using ConsolidadoDiario.Infraestrutura.Persistencia.Entidade;

namespace ConsolidadoDiario.Aplicacao.Abstracao;

public interface IConsolidadoDiarioDao
{
    Task<SaldoConsolidado?> ObterPorDataAsync(DateOnly dataReferencia, CancellationToken cancellationToken);

    Task<bool> ProcessarLancamentoAsync(
        HistoricoLancamento historicoLancamento,
        DateOnly dataReferencia,
        CancellationToken cancellationToken
        );
}
