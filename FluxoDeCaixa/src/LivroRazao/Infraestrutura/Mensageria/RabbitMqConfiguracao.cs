namespace LivroRazao.Infraestrutura.Mensageria;

public sealed class RabbitMqConfiguracao
{
    public string Host { get; init; } = "localhost";
    public int Porta { get; init; } = 5672;
    public string Usuario { get; init; } = "guest";
    public string Senha { get; init; } = "guest";
    public string VirtualHost { get; init; } = "/";
    public string Exchange { get; init; } = "fluxocaixa.exchange";   
    public string RoutingKeyLancamentoCriado { get; init; } = "fluxocaixa.lancamento.criado";
    public string FilaLancamentoCriado { get; init; } = "fluxocaixa.lancamento.criado";

    public string DeadLetterExchange { get; init; } = "fluxocaixa.eventos.dlx";
    public string FilaLancamentoCriadoDlq { get; init; } = "fluxocaixa.lancamento.criado.dlq";
    public string RoutingKeyLancamentoCriadoDlq { get; init; } = "lancamento.criado.dlq";

    public const string Secao = "RabbitMq";
}
