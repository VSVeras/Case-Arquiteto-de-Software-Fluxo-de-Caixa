namespace LivroRazao.Dominio.Caixa;

public interface ILancamentoRepositorio
{
    Task<Lancamento?> ObterPorCorrelationIdAsync(Guid correlationId, CancellationToken cancellationToken);
    Task AdicionarAsync(Lancamento lancamento, CancellationToken cancellationToken);
    Task<Lancamento?> ObterPorIdAsync(long id, CancellationToken cancellationToken);
}
