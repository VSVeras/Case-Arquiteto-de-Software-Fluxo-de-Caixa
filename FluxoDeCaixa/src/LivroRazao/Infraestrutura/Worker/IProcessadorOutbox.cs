namespace LivroRazao.Infraestrutura.Worker;

public interface IProcessadorOutbox
{
    Task<bool> ProcessarProximoAsync(CancellationToken cancellationToken);
}
