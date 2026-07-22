using LivroRazao.Aplicacao.Abstracao;
using LivroRazao.Infraestrutura.Mensageria;
using Microsoft.Extensions.Options;

namespace LivroRazao.Infraestrutura.Worker;

public sealed class ProcessadorOutbox(
    ILancamentoOutboxRepositorio repositorio,
    IPublicadorDeEventos publicador,
    IUnidadeDeTrabalho unidadeDeTrabalho,
    IOptions<PublicadorOutboxConfiguracao> opcoes,
    IRegistroDeEvento registroDeEvento
    ) : IProcessadorOutbox
{
    private readonly PublicadorOutboxConfiguracao _configuracao = opcoes.Value;

    public async Task<bool> ProcessarProximoAsync(CancellationToken cancellationToken)
    {
        var evento = await repositorio.ReservarProximoAsync(
            _configuracao.IntervaloEmSegundos,
            _configuracao.TempoLimiteDeProcessamentoEmMinutos,
            cancellationToken
            );

        if (evento is null)
        {
            return false;
        }

        try
        {
            await publicador.PublicarAsync(
                evento.TipoEvento,
                evento.Payload,
                evento.CorrelationId,
                cancellationToken
                );

            evento.MarcarComoPublicado();

            await unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);

            registroDeEvento.Informacao(
                "Evento publicado no RabbitMQ. " +
                "OutboxId {OutboxId}. " +
                "CorrelationId {CorrelationId}.",
                evento.Id,
                evento.CorrelationId
                );

            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception excecao)
        {
            evento.RegistrarFalha(excecao.Message, _configuracao.LimiteDeTentativas);

            await unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);

            registroDeEvento.Aviso(
                excecao,
                "Falha ao publicar evento. " +
                "OutboxId {OutboxId}. " +
                "CorrelationId {CorrelationId}. " +
                "Tentativas {Tentativas}. " +
                "Próxima tentativa após {IntervaloEmSegundos} segundos.",
                evento.Id,
                evento.CorrelationId,
                evento.Tentativas,
                _configuracao.IntervaloEmSegundos
                );

            return true;
        }
    }
}
