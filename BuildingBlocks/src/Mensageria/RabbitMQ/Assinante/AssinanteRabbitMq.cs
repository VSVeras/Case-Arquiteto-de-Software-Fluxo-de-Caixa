using System.Text;
using Mensageria.Assinante;
using Mensageria.RabbitMQ.Configuracao;
using Mensageria.RabbitMQ.Conexao;
using Mensageria.RabbitMQ.Retry;
using Mensageria.RabbitMQ.Topologia;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
namespace Mensageria.RabbitMQ.Assinante;
public sealed class AssinanteRabbitMq(IRabbitMqConexao conexao, IInicializadorRabbitMq inicializador, IOptions<RabbitMqConfiguracao> opcoes, ILogger<AssinanteRabbitMq> logger) : IAssinante
{
    private readonly RabbitMqConfiguracao _c = opcoes.Value;
    public async Task AssinarAsync(IManipuladorDeMensagem manipulador, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(manipulador);
        await inicializador.InicializarAsync(cancellationToken);
        var connection = await conexao.ObterConexaoAsync(cancellationToken);
        var options = new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true);
        await using var canal = await connection.CreateChannelAsync(options, cancellationToken);
        await canal.BasicQosAsync(0, _c.QuantidadeDeMensagensEmProcessamento, false, cancellationToken);
        var consumidor = new AsyncEventingBasicConsumer(canal);
        consumidor.ReceivedAsync += async (_, a) => await ProcessarAsync(canal, manipulador, a, cancellationToken);
        await canal.BasicConsumeAsync(_c.Fila, autoAck: false, consumer: consumidor, cancellationToken: cancellationToken);
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
    }
    private async Task ProcessarAsync(IChannel canal, IManipuladorDeMensagem manipulador, BasicDeliverEventArgs a, CancellationToken ct)
    {
        try
        {
            var correlationId = Guid.TryParse(a.BasicProperties.CorrelationId, out var id) ? id : (Guid?)null;
            var headers = a.BasicProperties.Headers?.ToDictionary(x => x.Key, x => (object?)x.Value) ?? new Dictionary<string, object?>();
            var mensagem = new MensagemRecebida(correlationId, a.BasicProperties.Type, Encoding.UTF8.GetString(a.Body.Span), headers);
            var resultado = await manipulador.ProcessarAsync(mensagem, ct);
            switch (resultado.Tipo)
            {
                case TipoResultadoProcessamento.Sucesso:
                    await canal.BasicAckAsync(a.DeliveryTag, false, ct); break;
                case TipoResultadoProcessamento.ErroDefinitivo:
                    await canal.BasicRejectAsync(a.DeliveryTag, false, ct); break;
                case TipoResultadoProcessamento.ErroTransitorio:
                    await TratarTransitorioAsync(canal, a, headers, ct); break;
                default: throw new ArgumentOutOfRangeException(nameof(resultado.Tipo));
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Durante o desligamento, a mensagem não confirmada será reentregue pelo broker.
            await TentarReenfileirarAsync(canal, a.DeliveryTag);
        }
        catch (Exception ex)
        {
            // Não confirmar mensagens cujo processamento ou publicação do retry falhou.
            // NACK com requeue evita perda; se o canal estiver fechado, o broker reentrega.
            logger.LogError(ex, "Falha inesperada ao consumir mensagem RabbitMQ (delivery tag {DeliveryTag}).", a.DeliveryTag);
            await TentarReenfileirarAsync(canal, a.DeliveryTag);
        }
    }
    private async Task TentarReenfileirarAsync(IChannel canal, ulong deliveryTag)
    {
        try
        {
            if (canal.IsOpen)
                await canal.BasicNackAsync(deliveryTag, multiple: false, requeue: true, CancellationToken.None);
        }
        catch (Exception ex)
        {
            // O fechamento do canal/conexão faz o broker reentregar mensagens sem ACK.
            logger.LogWarning(ex, "Não foi possível enviar NACK da mensagem {DeliveryTag}; aguardando reentrega pelo broker.", deliveryTag);
        }
    }

    private async Task TratarTransitorioAsync(IChannel canal, BasicDeliverEventArgs a, IReadOnlyDictionary<string, object?> headers, CancellationToken ct)
    {
        var tentativas = ContadorDeTentativasRabbitMq.Obter(headers);
        if (tentativas < _c.QuantidadeMaximaDeTentativas)
        {
            await PublicadorRetryRabbitMq.PublicarAsync(canal, _c, a.Body, a.BasicProperties, ct);
            await canal.BasicAckAsync(a.DeliveryTag, false, ct);
            return;
        }
        await canal.BasicRejectAsync(a.DeliveryTag, false, ct);
    }
}
