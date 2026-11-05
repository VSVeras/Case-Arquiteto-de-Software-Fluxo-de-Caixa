using RabbitMQ.Client;
namespace Mensageria.RabbitMQ.Conexao;
public interface IRabbitMqConexao : IAsyncDisposable
{
    Task<IConnection> ObterConexaoAsync(CancellationToken cancellationToken = default);
}
