using LivroRazao.Infraestrutura.Persistencia.Entidade.Outbox;

namespace LivroRazao.Aplicacao.Abstracao;

public interface ILancamentoOutboxRepositorio
{
    Task AdicionarAsync(LancamentoOutbox evento, CancellationToken cancellationToken);

    Task<LancamentoOutbox?> ReservarProximoAsync(
        int intervaloEmSegundos,
        int tempoLimiteDeProcessamentoEmMinutos,
        CancellationToken cancellationToken);
}
