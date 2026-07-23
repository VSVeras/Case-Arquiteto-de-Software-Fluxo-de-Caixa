namespace ConsolidadoDiario.Infraestrutura.Mensageria;

public sealed class RabbitMqConfiguracao
{
    public string Host { get; init; } = "localhost";
    public int Porta { get; init; } = 5672;
    public string Usuario { get; init; } = "guest";
    public string Senha { get; init; } = "guest";
    public string VirtualHost { get; init; } = "/";
    public string Exchange { get; init; } = "fluxocaixa.eventos";
    public string RoutingKeyLancamentoCriado { get; init; } = "lancamento.criado";
    public string FilaLancamentoCriado { get; init; } = "fluxocaixa.lancamento.criado";
    public ushort QuantidadeDeMensagensEmProcessamento { get; init; } = 1;

    public const string Secao = "RabbitMq";
}
