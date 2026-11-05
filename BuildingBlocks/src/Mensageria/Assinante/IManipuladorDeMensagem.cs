namespace Mensageria.Assinante;
public interface IManipuladorDeMensagem
{
    Task<ResultadoDoProcessamento> ProcessarAsync(MensagemRecebida mensagem, CancellationToken cancellationToken);
}
