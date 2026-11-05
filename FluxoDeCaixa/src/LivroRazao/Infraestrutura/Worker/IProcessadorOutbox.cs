namespace LivroRazao.Infraestrutura.Worker;

public interface IProcessadorOutbox
{
    Task<bool> ProcessarLoteAsync(
        CancellationToken cancellationToken);
}
