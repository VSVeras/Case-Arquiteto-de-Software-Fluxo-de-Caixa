using LivroRazao.Infraestrutura.Persistencia.Entidade.Outbox;

namespace LivroRazao.Aplicacao.Abstracao;

public interface ILancamentoOutboxRepositorio
{
    Task AdicionarAsync(
        LancamentoOutbox evento,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<LancamentoOutbox>> ReservarLoteAsync(
        int intervaloEmSegundos,
        int tempoLimiteDeProcessamentoEmMinutos,
        int quantidadePorLote,
        CancellationToken cancellationToken);
}
