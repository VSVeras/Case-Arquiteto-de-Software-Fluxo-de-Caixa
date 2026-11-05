using LivroRazao.Aplicacao.Abstracao;
using LivroRazao.Infraestrutura.Persistencia;
using LivroRazao.Infraestrutura.Persistencia.Entidade.Outbox;
using Microsoft.EntityFrameworkCore;

namespace LivroRazao.Infraestrutura.Repositorio;

public sealed class LancamentoOutboxRepositorio(
    LivroRazaoContexto contexto) : ILancamentoOutboxRepositorio
{
    public async Task AdicionarAsync(
        LancamentoOutbox evento,
        CancellationToken cancellationToken)
    {
        await contexto.LancamentosOutbox.AddAsync(
            evento,
            cancellationToken);
    }

    public async Task<IReadOnlyCollection<LancamentoOutbox>> ReservarLoteAsync(
        int intervaloEmSegundos,
        int tempoLimiteDeProcessamentoEmMinutos,
        int quantidadePorLote,
        CancellationToken cancellationToken)
    {
        await using var transacao =
            await contexto.Database.BeginTransactionAsync(cancellationToken);

        var dataAtual = DateTimeOffset.UtcNow;

        var limiteDaProximaTentativa =
            dataAtual.AddSeconds(-intervaloEmSegundos);

        var limiteDaReserva =
            dataAtual.AddMinutes(-tempoLimiteDeProcessamentoEmMinutos);

        var eventos = await contexto.LancamentosOutbox
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
                 LIMIT {quantidadePorLote}
                """)
            .ToListAsync(cancellationToken);

        foreach (var evento in eventos)
        {
            evento.ReservarParaProcessamento();
        }

        if (eventos.Count > 0)
        {
            await contexto.SaveChangesAsync(cancellationToken);
        }

        await transacao.CommitAsync(cancellationToken);

        return eventos;
    }
}
