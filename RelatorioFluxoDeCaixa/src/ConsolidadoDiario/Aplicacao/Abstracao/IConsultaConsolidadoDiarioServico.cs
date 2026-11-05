using ConsolidadoDiario.Aplicacao.Dto;

namespace ConsolidadoDiario.Aplicacao.Abstracao;

public interface IConsultaConsolidadoDiarioServico
{
    Task<ConsolidadoDiarioResposta?> ObterPorDataAsync(DateOnly dataReferencia, CancellationToken cancellationToken);
}
