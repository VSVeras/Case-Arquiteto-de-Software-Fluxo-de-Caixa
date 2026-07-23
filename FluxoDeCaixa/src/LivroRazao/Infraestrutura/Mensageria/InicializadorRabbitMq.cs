using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace LivroRazao.Infraestrutura.Mensageria;

public sealed class InicializadorRabbitMq(
    IRabbitMqConexao rabbitMqConexao,
    IOptions<RabbitMqConfiguracao> opcoes
    ) : IInicializadorRabbitMq
{
    private readonly RabbitMqConfiguracao _configuracao = opcoes.Value;

    public void Inicializar()
    {
        var conexao = rabbitMqConexao.ObterConexao();

        using var canal = conexao.CreateModel();

        DeclararExchangePrincipal(canal);
        DeclararDeadLetterExchange(canal);
        DeclararDeadLetterQueue(canal);
        DeclararFilaPrincipal(canal);
    }

    private void DeclararExchangePrincipal(IModel canal)
    {
        canal.ExchangeDeclare(
            exchange: _configuracao.Exchange,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            arguments: null
            );
    }

    private void DeclararDeadLetterExchange(IModel canal)
    {
        canal.ExchangeDeclare(
            exchange: _configuracao.DeadLetterExchange,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            arguments: null
            );
    }

    private void DeclararDeadLetterQueue(IModel canal)
    {
        canal.QueueDeclare(
            queue: _configuracao.FilaLancamentoCriadoDlq,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null
            );

        canal.QueueBind(
            queue: _configuracao.FilaLancamentoCriadoDlq,
            exchange: _configuracao.DeadLetterExchange,
            routingKey: _configuracao.RoutingKeyLancamentoCriadoDlq,
            arguments: null
            );
    }

    private void DeclararFilaPrincipal(IModel canal)
    {
        var argumentos = new Dictionary<string, object>
        {
            ["x-dead-letter-exchange"] =
                _configuracao.DeadLetterExchange,

            ["x-dead-letter-routing-key"] =
                _configuracao.RoutingKeyLancamentoCriadoDlq
        };

        canal.QueueDeclare(
            queue: _configuracao.FilaLancamentoCriado,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: argumentos
            );

        canal.QueueBind(
            queue: _configuracao.FilaLancamentoCriado,
            exchange: _configuracao.Exchange,
            routingKey: _configuracao.RoutingKeyLancamentoCriado,
            arguments: null
            );
    }
}
