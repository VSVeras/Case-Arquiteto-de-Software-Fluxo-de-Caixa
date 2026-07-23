namespace LivroRazao.Infraestrutura.Mensageria;

public sealed class RabbitMqConfiguracao
{
    public const string Secao = "RabbitMq";

    public string Host { get; init; } = "localhost";

    public int Porta { get; init; } = 5672;

    public string Usuario { get; init; } = "guest";

    public string Senha { get; init; } = "guest";

    public string VirtualHost { get; init; } = "/";

    public string Exchange { get; init; } = "fluxocaixa.eventos";

    public string FilaLancamentoCriado { get; init; } =
        "fluxocaixa.lancamento.criado";

    public string RoutingKeyLancamentoCriado { get; init; } =
        "lancamento.criado";

    public string RetryExchange { get; init; } =
        "fluxocaixa.eventos.retry";

    public string FilaLancamentoCriadoRetry { get; init; } =
        "fluxocaixa.lancamento.criado.retry";

    public string RoutingKeyLancamentoCriadoRetry { get; init; } =
        "lancamento.criado.retry";

    public int RetryTempoEmMilissegundos { get; init; } =
        30000;

    public string DeadLetterExchange { get; init; } =
        "fluxocaixa.eventos.dlx";

    public string FilaLancamentoCriadoDlq { get; init; } =
        "fluxocaixa.lancamento.criado.dlq";

    public string RoutingKeyLancamentoCriadoDlq { get; init; } =
        "lancamento.criado.dlq";
}
