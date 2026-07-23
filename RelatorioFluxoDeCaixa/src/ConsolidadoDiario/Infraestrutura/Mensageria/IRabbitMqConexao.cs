using RabbitMQ.Client;

namespace ConsolidadoDiario.Infraestrutura.Mensageria;

public interface IRabbitMqConexao : IDisposable
{
    IConnection ObterConexao();
}
