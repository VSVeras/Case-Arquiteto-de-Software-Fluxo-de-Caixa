using ConsolidadoDiario.Aplicacao.Abstracao;
using ConsolidadoDiario.Aplicacao.EventosDeIntegracao;
using ConsolidadoDiario.Infraestrutura.Persistencia.Entidade;

namespace ConsolidadoDiario.Aplicacao.Servico;

public sealed class ProcessadorEventoLancamento(
    IConsolidadoDiarioDao consolidadoDiarioDao)
    : IProcessadorEventoLancamento
{
    public Task<bool> ProcessarAsync(
        EventoDeLancamentoCriado evento,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evento);
        ArgumentNullException.ThrowIfNull(evento.Payload);

        if (evento.TipoEvento != nameof(EventoDeLancamentoCriado))
        {
            throw new ArgumentException($"Tipo de evento inválido: '{evento.TipoEvento}'.", nameof(evento));
        }

        var tipo = evento.Payload.Tipo switch
        {
            "D" => TipoDeLancamento.Debito,
            "C" => TipoDeLancamento.Credito,
            _ => throw new ArgumentException($"Tipo de lançamento inválido: '{evento.Payload.Tipo}'.", nameof(evento))
        };

        var historicoLancamento = new HistoricoLancamento(
            correlationId: evento.CorrelationId,
            tipo: tipo,
            dataLancamento: evento.Payload.DataLancamento,
            valor: evento.Payload.Valor,
            descricao: evento.Payload.Descricao,
            dataProcessamento: DateTimeOffset.UtcNow
            );

        var dataReferencia = DateOnly.FromDateTime(evento.Payload.DataLancamento.DateTime);

        return consolidadoDiarioDao.ProcessarLancamentoAsync(
            historicoLancamento,
            dataReferencia,
            cancellationToken
            );
    }
}
