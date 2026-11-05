namespace Mensageria.Assinante;
public interface IAssinante
{
    Task AssinarAsync(IManipuladorDeMensagem manipulador, CancellationToken cancellationToken);
}
