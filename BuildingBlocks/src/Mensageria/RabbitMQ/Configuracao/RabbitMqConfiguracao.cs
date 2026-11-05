namespace Mensageria.RabbitMQ.Configuracao;
public sealed class RabbitMqConfiguracao
{
    public const string Secao = "RabbitMq";
    public string Host { get; init; } = "localhost";
    public int Porta { get; init; } = 5672;
    public string Usuario { get; init; } = string.Empty;
    public string Senha { get; init; } = string.Empty;
    public string VirtualHost { get; init; } = "/";
    public string NomeDaConexao { get; init; } = "buildingblocks-mensageria";
    public string Exchange { get; init; } = "fluxocaixa.eventos";
    public string RoutingKey { get; init; } = "lancamento.criado";
    public string Fila { get; init; } = "fluxocaixa.lancamento.criado";
    public ushort QuantidadeDeMensagensEmProcessamento { get; init; } = 5;
    public string RetryExchange { get; init; } = "fluxocaixa.eventos.retry";
    public string RetryRoutingKey { get; init; } = "lancamento.criado.retry";
    public string RetryFila { get; init; } = "fluxocaixa.lancamento.criado.retry";
    public int RetryTempoEmMilissegundos { get; init; } = 30000;
    public int QuantidadeMaximaDeTentativas { get; init; } = 3;
    public string DeadLetterExchange { get; init; } = "fluxocaixa.eventos.dlx";
    public string DeadLetterRoutingKey { get; init; } = "lancamento.criado.dlq";
    public string DeadLetterFila { get; init; } = "fluxocaixa.lancamento.criado.dlq";
}
