using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ConsolidadoDiario.Infraestrutura.Mensageria;

public sealed class RabbitMqHealthCheck(IRabbitMqConexao rabbitMqConexao)
    : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var conexao = rabbitMqConexao.ObterConexao();

            return Task.FromResult(
                conexao.IsOpen
                    ? HealthCheckResult.Healthy()
                    : HealthCheckResult.Unhealthy("A conexão com o RabbitMQ está fechada."));
        }
        catch (Exception excecao)
        {
            return Task.FromResult(
                HealthCheckResult.Unhealthy(
                    "Não foi possível conectar ao RabbitMQ.",
                    excecao));
        }
    }
}
