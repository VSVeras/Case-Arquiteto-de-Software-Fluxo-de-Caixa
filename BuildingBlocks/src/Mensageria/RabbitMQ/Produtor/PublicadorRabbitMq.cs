using System.Text;
using Mensageria.Produtor;
using Mensageria.RabbitMQ.Configuracao;
using Mensageria.RabbitMQ.Conexao;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
namespace Mensageria.RabbitMQ.Produtor;
public sealed class PublicadorRabbitMq(IRabbitMqConexao conexao, IOptions<RabbitMqConfiguracao> opcoes) : IPublicador
{
    private readonly RabbitMqConfiguracao _c = opcoes.Value;
    public async Task PublicarAsync(MensagemParaPublicacao mensagem, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mensagem); mensagem.Validar();
        var connection = await conexao.ObterConexaoAsync(cancellationToken);
        var channelOptions = new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true);
        await using var canal = await connection.CreateChannelAsync(channelOptions, cancellationToken);
        var propriedades = new BasicProperties { Persistent = true, CorrelationId = mensagem.CorrelationId.ToString(), Type = mensagem.Tipo, ContentType = "application/json" };
        await canal.BasicPublishAsync(_c.Exchange, _c.RoutingKey, mandatory: true, basicProperties: propriedades, body: Encoding.UTF8.GetBytes(mensagem.Conteudo), cancellationToken: cancellationToken);
    }
}
