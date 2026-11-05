namespace Mensageria.Produtor;
public interface IPublicador
{
    Task PublicarAsync(MensagemParaPublicacao mensagem, CancellationToken cancellationToken = default);
}
