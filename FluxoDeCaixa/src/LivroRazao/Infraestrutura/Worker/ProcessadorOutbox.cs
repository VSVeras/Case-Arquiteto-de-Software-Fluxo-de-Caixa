using LivroRazao.Aplicacao.Abstracao;
using Mensageria.Produtor;
using LivroRazao.Infraestrutura.Persistencia.Entidade.Outbox;
using Microsoft.Extensions.Options;

namespace LivroRazao.Infraestrutura.Worker;

public sealed class ProcessadorOutbox(
    ILancamentoOutboxRepositorio repositorio,
    IPublicador publicador,
    IUnidadeDeTrabalho unidadeDeTrabalho,
    IOptions<PublicadorOutboxConfiguracao> opcoes,
    IRegistroDeEvento registroDeEvento
    ) : IProcessadorOutbox
{
    private readonly PublicadorOutboxConfiguracao _configuracao = opcoes.Value;

    public async Task<bool> ProcessarLoteAsync(
        CancellationToken cancellationToken)
    {
        var eventos = await repositorio.ReservarLoteAsync(
            _configuracao.IntervaloEmSegundos,
            _configuracao.TempoLimiteDeProcessamentoEmMinutos,
            _configuracao.QuantidadePorLote,
            cancellationToken
            );

        if (eventos.Count == 0)
        {
            return false;
        }

        foreach (var evento in eventos)
        {
            await ProcessarEventoAsync(
                evento,
                cancellationToken
                );
        }

        await unidadeDeTrabalho.SalvarAlteracoesAsync(
            cancellationToken
            );

        return true;
    }

    private async Task ProcessarEventoAsync(
        LancamentoOutbox evento,
        CancellationToken cancellationToken)
    {
        try
        {
            await publicador.PublicarAsync(
                new MensagemParaPublicacao(
                    evento.CorrelationId,
                    evento.TipoEvento,
                    evento.Payload
                    ),
                cancellationToken
                );

            evento.MarcarComoPublicado();

            registroDeEvento.Informacao(
                "Evento publicado no RabbitMQ. " +
                "OutboxId {OutboxId}. " +
                "CorrelationId {CorrelationId}.",
                evento.Id,
                evento.CorrelationId
                );
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception excecao)
        {
            evento.RegistrarFalha(excecao.Message);

            if (evento.Tentativas % _configuracao.IntervaloDeAlertaDeTentativas == 0)
            {
                registroDeEvento.Erro(
                    excecao,
                    "Falha recorrente ao publicar evento. " +
                    "O evento permanecera pendente para nova tentativa. " +
                    "OutboxId {OutboxId}. " +
                    "CorrelationId {CorrelationId}. " +
                    "Tentativas {Tentativas}.",
                    evento.Id,
                    evento.CorrelationId,
                    evento.Tentativas
                    );

                return;
            }

            registroDeEvento.Aviso(
                excecao,
                "Falha ao publicar evento. " +
                "OutboxId {OutboxId}. " +
                "CorrelationId {CorrelationId}. " +
                "Tentativas {Tentativas}.",
                evento.Id,
                evento.CorrelationId,
                evento.Tentativas
                );
        }
    }
}
