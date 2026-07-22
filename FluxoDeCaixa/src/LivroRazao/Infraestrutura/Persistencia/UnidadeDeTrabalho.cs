using LivroRazao.Aplicacao.Abstracao;
using LivroRazao.Infraestrutura.Excecao;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LivroRazao.Infraestrutura.Persistencia;

public sealed class UnidadeDeTrabalho(LivroRazaoContexto contexto) : IUnidadeDeTrabalho
{
    public async Task ExecutarEmTransacaoAsync(
        Func<CancellationToken, Task> operacao,
        CancellationToken cancellationToken
        )
    {
        await using var transacao = await contexto.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            await operacao(cancellationToken);

            await transacao.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException excecao) when (EhViolacaoDeUnicidade(excecao))
        {
            await transacao.RollbackAsync(cancellationToken);

            throw new ConflitoDePersistenciaException(
                "Foi detectado um conflito de unicidade ao persistir os dados.",
                excecao
                );
        }
        catch
        {
            await transacao.RollbackAsync(cancellationToken);

            throw;
        }
    }

    public Task SalvarAlteracoesAsync(CancellationToken cancellationToken)
    {
        return contexto.SaveChangesAsync(cancellationToken);
    }

    public void LimparRastreamento()
    {
        contexto.ChangeTracker.Clear();
    }

    private static bool EhViolacaoDeUnicidade(DbUpdateException excecao)
    {
        return excecao.InnerException is PostgresException postgresException
            && postgresException.SqlState
                == PostgresErrorCodes.UniqueViolation;
    }
}
