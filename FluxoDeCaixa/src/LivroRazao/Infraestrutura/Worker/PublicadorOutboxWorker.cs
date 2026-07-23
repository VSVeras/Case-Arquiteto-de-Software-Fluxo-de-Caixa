using LivroRazao.Aplicacao.Abstracao;
using LivroRazao.Infraestrutura.Mensageria;
using Microsoft.Extensions.Options;

namespace LivroRazao.Infraestrutura.Worker;

public class PublicadorOutboxWorker(
    IServiceScopeFactory fabricaDeEscopo,
    IInicializadorRabbitMq inicializadorRabbitMq,
    IOptions<PublicadorOutboxConfiguracao> opcoes,
    IRegistroDeEvento registroDeEvento)
    : BackgroundService
{
    private readonly PublicadorOutboxConfiguracao _configuracao = opcoes.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        registroDeEvento.Informacao("Publicador Outbox iniciado.");

        var topologiaInicializada = false;

        while (!stoppingToken.IsCancellationRequested)
        {
            if (!topologiaInicializada)
            {
                try
                {
                    inicializadorRabbitMq.Inicializar();
                    topologiaInicializada = true;

                    registroDeEvento.Informacao("Topologia do RabbitMQ inicializada.");
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception excecao)
                {
                    registroDeEvento.Aviso(
                        excecao,
                        "RabbitMQ indisponível. Nova tentativa em {IntervaloEmSegundos} segundos.",
                        _configuracao.IntervaloEmSegundos
                    );

                    if (!await AguardarNovaTentativaAsync(stoppingToken))
                    {
                        break;
                    }

                    continue;
                }
            }

            var deveAguardar = false;

            try
            {
                using var escopo = fabricaDeEscopo.CreateScope();

                var processador = escopo.ServiceProvider.GetRequiredService<IProcessadorOutbox>();

                var encontrouEvento = await processador.ProcessarProximoAsync(stoppingToken);

                deveAguardar = !encontrouEvento;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception excecao)
            {
                registroDeEvento.Erro(
                    excecao,
                    "Erro não tratado durante o processamento da Outbox."
                );

                deveAguardar = true;
            }

            if (!deveAguardar)
            {
                continue;
            }

            if (!await AguardarNovaTentativaAsync(stoppingToken))
            {
                break;
            }
        }

        registroDeEvento.Informacao("Publicador Outbox finalizado.");
    }

    private async Task<bool> AguardarNovaTentativaAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(_configuracao.IntervaloEmSegundos),stoppingToken
            );

            return true;
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return false;
        }
    }
}
