using Microsoft.Extensions.Options;

namespace ConsolidadoDiario.Infraestrutura.Mensageria;

public sealed class InicializadorRabbitMq(
    IRabbitMqConexao rabbitMqConexao,
    IOptions<RabbitMqConfiguracao> opcoes)
    : IInicializadorRabbitMq
{
    private readonly RabbitMqConfiguracao _configuracao = opcoes.Value;

    public void Inicializar()
    {
        var conexao = rabbitMqConexao.ObterConexao();

        using var canal = conexao.CreateModel();

        canal.ExchangeDeclarePassive(
            _configuracao.Exchange
        );

        canal.QueueDeclarePassive(
            _configuracao.FilaLancamentoCriado
        );
    }
}
