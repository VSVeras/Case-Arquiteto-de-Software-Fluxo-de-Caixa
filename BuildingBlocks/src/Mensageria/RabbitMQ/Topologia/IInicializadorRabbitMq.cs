namespace Mensageria.RabbitMQ.Topologia;
public interface IInicializadorRabbitMq { Task InicializarAsync(CancellationToken cancellationToken = default); }
