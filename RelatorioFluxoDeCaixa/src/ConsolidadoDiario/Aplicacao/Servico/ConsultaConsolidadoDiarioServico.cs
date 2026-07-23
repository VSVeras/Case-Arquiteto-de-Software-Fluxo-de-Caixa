using ConsolidadoDiario.Aplicacao.Abstracao;
using ConsolidadoDiario.Aplicacao.Dto;

namespace ConsolidadoDiario.Aplicacao.Servico;

public sealed class ConsultaConsolidadoDiarioServico(IConsolidadoDiarioDao consolidadoDiarioDao)
    : IConsultaConsolidadoDiarioServico
{
    public async Task<ConsolidadoDiarioResposta?> ObterPorDataAsync(DateOnly dataReferencia, CancellationToken cancellationToken)
    {
        if (dataReferencia == default)
        {
            throw new ArgumentException("A data de referência deve ser informada.", nameof(dataReferencia));
        }

        var saldoConsolidado = await consolidadoDiarioDao.ObterPorDataAsync(dataReferencia, cancellationToken);
        if (saldoConsolidado is null)
        {
            return null;
        }

        return new ConsolidadoDiarioResposta(
            DateOnly.FromDateTime(saldoConsolidado.DataReferencia),
            saldoConsolidado.TotalCreditos,
            saldoConsolidado.TotalDebitos,
            saldoConsolidado.SaldoDiarioConsolidado,
            new DateTimeOffset(saldoConsolidado.DataAtualizacao, TimeSpan.Zero
            )
        );
    }
}
