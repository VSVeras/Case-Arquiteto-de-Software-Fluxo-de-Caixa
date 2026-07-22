using System.Text;
using Microsoft.Extensions.Options;

namespace LivroRazao.Infraestrutura.Mensageria;

public sealed class PublicadorRabbitMq(
    IRabbitMqConexao rabbitMqConexao,
    IOptions<RabbitMqConfiguracao> opcoes
    ) : IPublicadorDeEventos
{
    private readonly RabbitMqConfiguracao _configuracao = opcoes.Value;

    public Task PublicarAsync(
        string tipoEvento,
        string payload,
        Guid correlationId,
        CancellationToken cancellationToken
        )
    {
        cancellationToken.ThrowIfCancellationRequested();

        // RabbitMQ.Client 6.8.1 internamente usa operações síncronas e retorna.
        // Não é recomendado usar Task.Run, pois apenas deslocaria o bloqueio para outra thread.
        var conexao = rabbitMqConexao.ObterConexao();

        using var canal = conexao.CreateModel();

        canal.ConfirmSelect();

        var propriedades = canal.CreateBasicProperties();
        propriedades.Persistent = true;
        propriedades.CorrelationId = correlationId.ToString();
        propriedades.Type = tipoEvento;
        propriedades.ContentType = "application/json";

        var corpo = Encoding.UTF8.GetBytes(payload);

        canal.BasicPublish(
            exchange: _configuracao.Exchange,
            routingKey: _configuracao.RoutingKeyLancamentoCriado,
            mandatory: true,
            basicProperties: propriedades,
            body: corpo
            );

        canal.WaitForConfirmsOrDie(TimeSpan.FromSeconds(10));

        return Task.CompletedTask;
    }
}
