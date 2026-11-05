using Mensageria.RabbitMQ.Configuracao;
using Mensageria.RabbitMQ.Conexao;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
namespace Mensageria.RabbitMQ.Topologia;
public sealed class InicializadorRabbitMq(IRabbitMqConexao conexao, IOptions<RabbitMqConfiguracao> opcoes) : IInicializadorRabbitMq
{
    private readonly RabbitMqConfiguracao _c = opcoes.Value;
    public async Task InicializarAsync(CancellationToken cancellationToken = default)
    {
        var connection = await conexao.ObterConexaoAsync(cancellationToken);
        await using var canal = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        await canal.ExchangeDeclareAsync(_c.Exchange, ExchangeType.Topic, true, false, cancellationToken: cancellationToken);
        await canal.ExchangeDeclareAsync(_c.RetryExchange, ExchangeType.Direct, true, false, cancellationToken: cancellationToken);
        await canal.ExchangeDeclareAsync(_c.DeadLetterExchange, ExchangeType.Direct, true, false, cancellationToken: cancellationToken);
        await canal.QueueDeclareAsync(_c.DeadLetterFila, true, false, false, cancellationToken: cancellationToken);
        await canal.QueueBindAsync(_c.DeadLetterFila, _c.DeadLetterExchange, _c.DeadLetterRoutingKey, cancellationToken: cancellationToken);
        var retryArgs = new Dictionary<string, object?> { ["x-message-ttl"] = _c.RetryTempoEmMilissegundos, ["x-dead-letter-exchange"] = _c.Exchange, ["x-dead-letter-routing-key"] = _c.RoutingKey };
        await canal.QueueDeclareAsync(_c.RetryFila, true, false, false, retryArgs, cancellationToken: cancellationToken);
        await canal.QueueBindAsync(_c.RetryFila, _c.RetryExchange, _c.RetryRoutingKey, cancellationToken: cancellationToken);
        var principalArgs = new Dictionary<string, object?> { ["x-dead-letter-exchange"] = _c.DeadLetterExchange, ["x-dead-letter-routing-key"] = _c.DeadLetterRoutingKey };
        await canal.QueueDeclareAsync(_c.Fila, true, false, false, principalArgs, cancellationToken: cancellationToken);
        await canal.QueueBindAsync(_c.Fila, _c.Exchange, _c.RoutingKey, cancellationToken: cancellationToken);
    }
}
