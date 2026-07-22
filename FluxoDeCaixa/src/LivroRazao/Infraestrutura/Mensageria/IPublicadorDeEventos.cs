namespace LivroRazao.Infraestrutura.Mensageria;

public interface IPublicadorDeEventos
{
    Task PublicarAsync(
        string tipoEvento,
        string payload,
        Guid correlationId,
        CancellationToken cancellationToken
        );
}
