using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace LivroRazao.Infraestrutura.Mensageria;

public sealed class RabbitMqHealthCheck(
    IRabbitMqConexao rabbitMqConexao,
    IOptions<RabbitMqConfiguracao> opcoes
    ) : IHealthCheck
{
    private readonly RabbitMqConfiguracao _configuracao = opcoes.Value;

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default
        )
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var conexao = rabbitMqConexao.ObterConexao();

            if (!conexao.IsOpen)
            {
                return Task.FromResult(
                    HealthCheckResult.Unhealthy("A conexao com o RabbitMQ esta fechada.")
                    );
            }

            using var canal = conexao.CreateModel();

            if (!canal.IsOpen)
            {
                return Task.FromResult(
                    HealthCheckResult.Unhealthy("Nao foi possivel abrir um canal no RabbitMQ.")
                    );
            }

            canal.ExchangeDeclarePassive(_configuracao.Exchange);

            canal.QueueDeclarePassive(_configuracao.FilaLancamentoCriado);

            return Task.FromResult(
                HealthCheckResult.Healthy("RabbitMQ disponivel e topologia validada.")
                );
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception excecao)
        {
            return Task.FromResult(
                HealthCheckResult.Unhealthy(
                    "Falha ao validar a disponibilidade do RabbitMQ.",
                    excecao
                    )
                );
        }
    }
}
