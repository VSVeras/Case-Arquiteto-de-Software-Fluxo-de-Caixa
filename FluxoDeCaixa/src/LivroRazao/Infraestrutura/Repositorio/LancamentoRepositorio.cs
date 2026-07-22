using LivroRazao.Dominio.Caixa;
using LivroRazao.Infraestrutura.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace LivroRazao.Infraestrutura.Repositorio;

public sealed class LancamentoRepositorio(LivroRazaoContexto contexto) : ILancamentoRepositorio
{
    public async Task<Lancamento?> ObterPorCorrelationIdAsync(
    Guid correlationId,
    CancellationToken cancellationToken)
    {
        return await contexto.Lancamentos
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.CorrelationId == correlationId,
                cancellationToken
                );
    }

    public async Task AdicionarAsync(Lancamento lancamento, CancellationToken cancellationToken)
    {
        await contexto.Lancamentos.AddAsync(
            lancamento,
            cancellationToken
            );
    }

    public async Task<Lancamento?> ObterPorIdAsync(long id, CancellationToken cancellationToken)
    {
        return await contexto.Lancamentos
            .AsNoTracking()
            .SingleOrDefaultAsync(
                lancamento => lancamento.Id == id,
                cancellationToken
                );
    }
}
