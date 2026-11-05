using ConsolidadoDiario.Aplicacao.Abstracao;
using Mensageria.Assinante;

namespace ConsolidadoDiario.Infraestrutura.Worker;

public sealed class ConsumidorConsolidadoWorker(
    IAssinante assinante,
    IManipuladorDeMensagem manipulador,
    IRegistroDeEvento registroDeEvento)
    : BackgroundService
{
    private static readonly TimeSpan AtrasoMaximo = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        registroDeEvento.Informacao("Consumidor do consolidado diário iniciado.");
        var atraso = TimeSpan.FromSeconds(1);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await assinante.AssinarAsync(manipulador, stoppingToken);
                    // Uma assinatura que terminou sem cancelamento também deve ser restabelecida.
                    if (!stoppingToken.IsCancellationRequested)
                        registroDeEvento.Aviso(null, "Assinatura RabbitMQ encerrada inesperadamente. Nova tentativa em {AtrasoSegundos}s.", atraso.TotalSeconds);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    // A assinatura deve continuar disponível mesmo para falhas não classificadas
                    // pela biblioteca RabbitMQ; o backoff impede loop agressivo.
                    registroDeEvento.Aviso(ex, "Falha na assinatura RabbitMQ. Nova tentativa em {AtrasoSegundos}s.", atraso.TotalSeconds);
                }

                await Task.Delay(atraso, stoppingToken);
                atraso = TimeSpan.FromSeconds(Math.Min(atraso.TotalSeconds * 2, AtrasoMaximo.TotalSeconds));
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Encerramento normal do host, inclusive durante o backoff.
        }
        finally
        {
            registroDeEvento.Informacao("Consumidor do consolidado diário finalizado.");
        }
    }

}
