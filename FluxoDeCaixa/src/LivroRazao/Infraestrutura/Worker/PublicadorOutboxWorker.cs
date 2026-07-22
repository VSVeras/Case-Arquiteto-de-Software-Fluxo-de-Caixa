using LivroRazao.Aplicacao.Abstracao;
using Microsoft.Extensions.Options;

namespace LivroRazao.Infraestrutura.Worker;

public class PublicadorOutboxWorker(
    IServiceScopeFactory fabricaDeEscopo,
    IOptions<PublicadorOutboxConfiguracao> opcoes,
    IRegistroDeEvento registroDeEvento)
    : BackgroundService
{
    private readonly PublicadorOutboxConfiguracao _configuracao = opcoes.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        registroDeEvento.Informacao("Publicador Outbox iniciado.");

        while (!stoppingToken.IsCancellationRequested)
        {
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

            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(_configuracao.IntervaloEmSegundos),
                    stoppingToken
                    );
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        registroDeEvento.Informacao("Publicador Outbox finalizado.");
    }
}
