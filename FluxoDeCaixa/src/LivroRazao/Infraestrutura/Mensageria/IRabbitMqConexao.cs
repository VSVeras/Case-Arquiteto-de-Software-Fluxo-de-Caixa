using RabbitMQ.Client;

namespace LivroRazao.Infraestrutura.Mensageria;

public interface IRabbitMqConexao : IDisposable
{
    IConnection ObterConexao();
}
