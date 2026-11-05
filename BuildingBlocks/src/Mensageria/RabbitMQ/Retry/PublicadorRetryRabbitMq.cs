using Mensageria.RabbitMQ.Configuracao;
using RabbitMQ.Client;

namespace Mensageria.RabbitMQ.Retry;

internal static class PublicadorRetryRabbitMq
{
    public static async Task PublicarAsync(
        IChannel canal,
        RabbitMqConfiguracao configuracao,
        ReadOnlyMemory<byte> corpo,
        IReadOnlyBasicProperties propriedadesOriginais,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(canal);
        ArgumentNullException.ThrowIfNull(configuracao);
        ArgumentNullException.ThrowIfNull(propriedadesOriginais);

        var propriedadesParaPublicacao =
            new BasicProperties(propriedadesOriginais);

        await canal.BasicPublishAsync(
            exchange: configuracao.RetryExchange,
            routingKey: configuracao.RetryRoutingKey,
            mandatory: true,
            basicProperties: propriedadesParaPublicacao,
            body: corpo,
            cancellationToken: cancellationToken);
    }
}
