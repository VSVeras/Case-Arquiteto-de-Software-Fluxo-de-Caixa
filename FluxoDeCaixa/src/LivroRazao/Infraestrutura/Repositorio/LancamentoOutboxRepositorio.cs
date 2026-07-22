using LivroRazao.Aplicacao.Abstracao;
using LivroRazao.Infraestrutura.Persistencia;
using LivroRazao.Infraestrutura.Persistencia.Entidades.Outbox;
using Microsoft.EntityFrameworkCore;

namespace LivroRazao.Infraestrutura.Repositorio;

public sealed class LancamentoOutboxRepositorio(LivroRazaoContexto contexto) : ILancamentoOutboxRepositorio
{
    public async Task AdicionarAsync(
        LancamentoOutbox evento,
        CancellationToken cancellationToken)
    {
        await contexto.LancamentosOutbox.AddAsync(
            evento,
            cancellationToken
            );
    }

    public async Task<LancamentoOutbox?> ReservarProximoAsync(
        int intervaloEmSegundos,
        int tempoLimiteDeProcessamentoEmMinutos,
        CancellationToken cancellationToken)
    {
        await using var transacao = await contexto.Database.BeginTransactionAsync(cancellationToken);

        var dataAtual = DateTimeOffset.UtcNow;
        var limiteDaProximaTentativa = dataAtual.AddSeconds(-intervaloEmSegundos);
        var limiteDaReserva = dataAtual.AddMinutes(-tempoLimiteDeProcessamentoEmMinutos);

        var evento = await contexto.LancamentosOutbox
            .FromSqlInterpolated(
                $"""
                SELECT *
                  FROM "TB_LANCAMENTO_OUTBOX"
                 WHERE
                    (
                           "STATUS" = {(int)StatusDaOutbox.Pendente}
                       AND
                       (
                              "DATA_ULTIMA_TENTATIVA" IS NULL
                           OR "DATA_ULTIMA_TENTATIVA" <= {limiteDaProximaTentativa}
                       )
                    )
                    OR
                    (
                           "STATUS" = {(int)StatusDaOutbox.Processando}
                       AND "DATA_ULTIMA_TENTATIVA" < {limiteDaReserva}
                    )
                 ORDER BY "DATA_CRIACAO"
                 FOR UPDATE SKIP LOCKED
                 LIMIT 1
                """)
            .SingleOrDefaultAsync(cancellationToken);

        if (evento is null)
        {
            await transacao.CommitAsync(cancellationToken);

            return null;
        }

        evento.ReservarParaProcessamento();

        await contexto.SaveChangesAsync(cancellationToken);

        await transacao.CommitAsync(cancellationToken);

        return evento;
    }
}
