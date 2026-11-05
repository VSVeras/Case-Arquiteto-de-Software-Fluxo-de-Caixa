using Mensageria.RabbitMQ.Configuracao;
using Mensageria.RabbitMQ.Conexao;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
namespace Mensageria.RabbitMQ.HealthCheck;
public sealed class RabbitMqHealthCheck(IRabbitMqConexao conexao, IOptions<RabbitMqConfiguracao> opcoes) : IHealthCheck
{
    private readonly RabbitMqConfiguracao _c = opcoes.Value;
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var connection = await conexao.ObterConexaoAsync(cancellationToken);
            if (!connection.IsOpen) return HealthCheckResult.Unhealthy("A conexao com o RabbitMQ esta fechada.");
            await using var canal = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
            await canal.ExchangeDeclarePassiveAsync(_c.Exchange, cancellationToken);
            await canal.QueueDeclarePassiveAsync(_c.Fila, cancellationToken);
            return HealthCheckResult.Healthy("RabbitMQ disponivel e topologia validada.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception e) { return HealthCheckResult.Unhealthy("Falha ao validar a disponibilidade do RabbitMQ.", e); }
    }
}
