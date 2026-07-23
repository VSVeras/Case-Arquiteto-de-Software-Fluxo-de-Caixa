using System.Text.Json;
using ConsolidadoDiario.Aplicacao.Abstracao;
using ConsolidadoDiario.Aplicacao.EventosDeIntegracao;
using ConsolidadoDiario.Infraestrutura.Mensageria;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ConsolidadoDiario.Infraestrutura.Worker;

public sealed class ConsumidorConsolidadoWorker(
    IRabbitMqConexao rabbitMqConexao,
    IInicializadorRabbitMq inicializadorRabbitMq,
    IServiceScopeFactory fabricaDeEscopo,
    IOptions<RabbitMqConfiguracao> opcoes,
    IRegistroDeEvento registroDeEvento)
    : BackgroundService
{
    private static readonly JsonSerializerOptions OpcoesDeSerializacao = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly TimeSpan IntervaloEntreTentativas = TimeSpan.FromSeconds(5);

    private readonly RabbitMqConfiguracao _configuracao = opcoes.Value;

    private IModel? _canal;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        registroDeEvento.Informacao("Consumidor do consolidado diário iniciado.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                inicializadorRabbitMq.Inicializar();

                registroDeEvento.Informacao("Topologia do RabbitMQ inicializada.");

                var conexao = rabbitMqConexao.ObterConexao();

                registroDeEvento.Informacao("Conexão com RabbitMQ obtida.");

                _canal = conexao.CreateModel();

                registroDeEvento.Informacao("Canal RabbitMQ criado.");

                _canal.BasicQos(prefetchSize: 0, prefetchCount: _configuracao.QuantidadeDeMensagensEmProcessamento, global: false);

                var consumidor = new AsyncEventingBasicConsumer(_canal);

                consumidor.Received += (_, argumentos) => ProcessarMensagemAsync(argumentos, stoppingToken);

                _canal.BasicConsume(queue: _configuracao.FilaLancamentoCriado, autoAck: false, consumer: consumidor);

                registroDeEvento.Informacao(
                    "Consumidor iniciado. Fila {Fila}.",
                    _configuracao.FilaLancamentoCriado
                );

                await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception excecao)
            {
                registroDeEvento.Aviso(
                    excecao,
                    "RabbitMQ indisponível. Nova tentativa em 5 segundos."
                );

                DescartarCanal();

                if (!await AguardarNovaTentativaAsync(stoppingToken))
                {
                    break;
                }
            }
        }

        DescartarCanal();

        registroDeEvento.Informacao("Consumidor do consolidado diário finalizado.");
    }

    public override void Dispose()
    {
        DescartarCanal();

        base.Dispose();
    }

    private async Task ProcessarMensagemAsync(BasicDeliverEventArgs argumentos, CancellationToken cancellationToken)
    {
        if (_canal is not { IsOpen: true })
        {
            return;
        }

        try
        {
            var evento = JsonSerializer.Deserialize<EventoDeLancamentoCriado>(argumentos.Body.Span, OpcoesDeSerializacao)
                ?? throw new JsonException("O conteúdo da mensagem está vazio.");

            ValidarMetadadosDaMensagem(evento, argumentos);

            using var escopo = fabricaDeEscopo.CreateScope();

            var processador = escopo.ServiceProvider.GetRequiredService<IProcessadorEventoLancamento>();

            var foiProcessado = await processador.ProcessarAsync(evento, cancellationToken);

            _canal.BasicAck(argumentos.DeliveryTag, multiple: false);

            registroDeEvento.Informacao(
                foiProcessado
                    ? "Lançamento consolidado. CorrelationId {CorrelationId}."
                    : "Lançamento já processado. CorrelationId {CorrelationId}.",
                evento.CorrelationId
            );
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            if (_canal is { IsOpen: true })
            {
                _canal.BasicNack(argumentos.DeliveryTag, multiple: false, requeue: true);
            }
        }
        catch (JsonException excecao)
        {
            registroDeEvento.Aviso(
                excecao,
                "Mensagem inválida descartada. DeliveryTag {DeliveryTag}.",
                argumentos.DeliveryTag
            );

            if (_canal is { IsOpen: true })
            {
                _canal.BasicReject(argumentos.DeliveryTag, requeue: false);
            }
        }
        catch (ArgumentException excecao)
        {
            registroDeEvento.Aviso(
                excecao,
                "Evento de lançamento inválido descartado. DeliveryTag {DeliveryTag}.",
                argumentos.DeliveryTag
            );

            if (_canal is { IsOpen: true })
            {
                _canal.BasicReject(argumentos.DeliveryTag, requeue: false);
            }
        }
        catch (Exception excecao)
        {
            registroDeEvento.Erro(
                excecao,
                "Erro ao processar mensagem do consolidado. DeliveryTag {DeliveryTag}.",
                argumentos.DeliveryTag
            );

            if (_canal is { IsOpen: true })
            {
                _canal.BasicNack(argumentos.DeliveryTag, multiple: false, requeue: true);
            }
        }
    }

    private async Task<bool> AguardarNovaTentativaAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(IntervaloEntreTentativas, stoppingToken);

            return true;
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return false;
        }
    }

    private void DescartarCanal()
    {
        if (_canal is null)
        {
            return;
        }

        try
        {
            if (_canal.IsOpen)
            {
                _canal.Close();
            }
        }
        catch (Exception excecao)
        {
            registroDeEvento.Aviso(
                excecao,
                "Não foi possível fechar o canal RabbitMQ normalmente."
            );
        }
        finally
        {
            _canal.Dispose();
            _canal = null;
        }
    }

    private static void ValidarMetadadosDaMensagem(EventoDeLancamentoCriado evento, BasicDeliverEventArgs argumentos)
    {
        if (evento.CorrelationId == Guid.Empty)
        {
            throw new ArgumentException("O CorrelationId do evento deve ser informado.");
        }

        var correlationIdDaMensagem = argumentos.BasicProperties.CorrelationId;

        if (!string.IsNullOrWhiteSpace(correlationIdDaMensagem)
            && (!Guid.TryParse(
                    correlationIdDaMensagem, out var correlationId) || correlationId != evento.CorrelationId))
        {
            throw new ArgumentException("O CorrelationId do cabeçalho da mensagem não corresponde ao evento.");
        }

        var tipoDaMensagem = argumentos.BasicProperties.Type;

        if (!string.IsNullOrWhiteSpace(tipoDaMensagem)
            && !string.Equals(tipoDaMensagem, evento.TipoEvento, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "O tipo do cabeçalho da mensagem não corresponde ao evento.");
        }
    }
}
