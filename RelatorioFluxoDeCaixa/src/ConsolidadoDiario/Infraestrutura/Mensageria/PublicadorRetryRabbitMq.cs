using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace ConsolidadoDiario.Infraestrutura.Mensageria;

public sealed class PublicadorRetryRabbitMq(
    IOptions<RabbitMqConfiguracao> opcoes)
    : IPublicadorRetryRabbitMq
{
    private readonly RabbitMqConfiguracao _configuracao = opcoes.Value;

    public void Publicar(
        IModel canal,
        ReadOnlyMemory<byte> corpo,
        IBasicProperties propriedades)
    {
        ArgumentNullException.ThrowIfNull(canal);
        ArgumentNullException.ThrowIfNull(propriedades);

        canal.ConfirmSelect();

        canal.BasicPublish(
            exchange: _configuracao.RetryExchange,
            routingKey: _configuracao.RoutingKeyLancamentoCriadoRetry,
            mandatory: false,
            basicProperties: propriedades,
            body: corpo);

        canal.WaitForConfirmsOrDie(
            TimeSpan.FromSeconds(10));
    }
}
