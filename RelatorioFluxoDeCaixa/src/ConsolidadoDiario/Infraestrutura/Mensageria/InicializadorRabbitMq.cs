using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace ConsolidadoDiario.Infraestrutura.Mensageria;

public sealed class InicializadorRabbitMq(
    IRabbitMqConexao rabbitMqConexao,
    IOptions<RabbitMqConfiguracao> opcoes)
    : IInicializadorRabbitMq
{
    private readonly RabbitMqConfiguracao _configuracao = opcoes.Value;

    public void Inicializar()
    {
        var conexao = rabbitMqConexao.ObterConexao();

        using var canal = conexao.CreateModel();

        canal.ExchangeDeclare(
            exchange: _configuracao.Exchange,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            arguments: null);

        canal.QueueDeclare(
            queue: _configuracao.FilaLancamentoCriado,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        canal.QueueBind(
            queue: _configuracao.FilaLancamentoCriado,
            exchange: _configuracao.Exchange,
            routingKey: _configuracao.RoutingKeyLancamentoCriado,
            arguments: null);
    }
}
