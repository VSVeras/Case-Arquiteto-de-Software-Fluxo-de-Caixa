using System.Text.Json;
using ConsolidadoDiario.Aplicacao.Abstracao;
using ConsolidadoDiario.Aplicacao.EventosDeIntegracao;
using Mensageria.Assinante;

namespace ConsolidadoDiario.Infraestrutura.Worker;

public sealed class ManipuladorEventoLancamento(
    IServiceScopeFactory fabricaDeEscopo,
    IRegistroDeEvento registroDeEvento)
    : IManipuladorDeMensagem
{
    private static readonly JsonSerializerOptions OpcoesDeSerializacao =
        new(JsonSerializerDefaults.Web);

    public async Task<ResultadoDoProcessamento> ProcessarAsync(
        MensagemRecebida mensagem,
        CancellationToken cancellationToken)
    {
        try
        {
            var evento = JsonSerializer.Deserialize<EventoDeLancamentoCriado>(
                mensagem.Conteudo,
                OpcoesDeSerializacao)
                ?? throw new JsonException("O conteúdo da mensagem está vazio.");

            ValidarMetadadosDaMensagem(evento, mensagem);

            using var escopo = fabricaDeEscopo.CreateScope();
            var processador = escopo.ServiceProvider
                .GetRequiredService<IProcessadorEventoLancamento>();

            var foiProcessado = await processador.ProcessarAsync(
                evento,
                cancellationToken);

            registroDeEvento.Informacao(
                foiProcessado
                    ? "Lançamento consolidado. CorrelationId {CorrelationId}."
                    : "Lançamento já processado. CorrelationId {CorrelationId}.",
                evento.CorrelationId);

            return ResultadoDoProcessamento.Sucesso();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (JsonException excecao)
        {
            registroDeEvento.Aviso(
                excecao,
                "Mensagem inválida descartada. CorrelationId {CorrelationId}.",
                mensagem.CorrelationId);

            return ResultadoDoProcessamento.ErroDefinitivo(excecao);
        }
        catch (ArgumentException excecao)
        {
            registroDeEvento.Aviso(
                excecao,
                "Evento de lançamento inválido descartado. CorrelationId {CorrelationId}.",
                mensagem.CorrelationId);

            return ResultadoDoProcessamento.ErroDefinitivo(excecao);
        }
        catch (Exception excecao)
        {
            registroDeEvento.Erro(
                excecao,
                "Erro transitório ao processar mensagem. CorrelationId {CorrelationId}.",
                mensagem.CorrelationId);

            return ResultadoDoProcessamento.ErroTransitorio(excecao);
        }
    }

    private static void ValidarMetadadosDaMensagem(
        EventoDeLancamentoCriado evento,
        MensagemRecebida mensagem)
    {
        if (evento.CorrelationId == Guid.Empty)
        {
            throw new ArgumentException(
                "O CorrelationId do evento deve ser informado.");
        }

        if (mensagem.CorrelationId.HasValue
            && mensagem.CorrelationId.Value != evento.CorrelationId)
        {
            throw new ArgumentException(
                "O CorrelationId do cabeçalho da mensagem não corresponde ao evento.");
        }

        if (!string.IsNullOrWhiteSpace(mensagem.Tipo)
            && !string.Equals(
                mensagem.Tipo,
                evento.TipoEvento,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "O tipo do cabeçalho da mensagem não corresponde ao evento.");
        }
    }
}
