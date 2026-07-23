using RabbitMQ.Client;

namespace ConsolidadoDiario.Infraestrutura.Mensageria;

public interface IPublicadorRetryRabbitMq
{
    void Publicar(
        IModel canal,
        ReadOnlyMemory<byte> corpo,
        IBasicProperties propriedades);
}
