using ConsolidadoDiario.Aplicacao.Abstracao;
using ConsolidadoDiario.Infraestrutura.Persistencia.Entidade;
using Dapper;
using Npgsql;

namespace ConsolidadoDiario.Infraestrutura.Dao;

public sealed class ConsolidadoDiarioDao(NpgsqlDataSource dataSource)
    : IConsolidadoDiarioDao
{

    private const string SqlObterPorData = """
        SELECT
            "DATA_REFERENCIA"::timestamp AS "DataReferencia",
            "TOTAL_CREDITOS" AS "TotalCreditos",
            "TOTAL_DEBITOS" AS "TotalDebitos",
            "SALDO_DIARIO_CONSOLIDADO" AS "SaldoDiarioConsolidado",
            "DATA_ATUALIZACAO" AS "DataAtualizacao"
        FROM "TB_SALDO_CONSOLIDADO_DIARIO"
        WHERE "DATA_REFERENCIA" = @DataReferencia;
        """;

    private const string SqlProcessarLancamento = """
        WITH historico_inserido AS
        (
            INSERT INTO "TB_HISTORICO_LANCAMENTO"
            (
                "CORRELATION_ID",
                "TIPO",
                "DATA_LANCAMENTO",
                "VALOR",
                "DESCRICAO",
                "DATA_PROCESSAMENTO"
            )
            VALUES
            (
                @CorrelationId,
                @Tipo,
                @DataLancamento,
                @Valor,
                @Descricao,
                @DataProcessamento
            )
            ON CONFLICT ("CORRELATION_ID") DO NOTHING
            RETURNING 1
        )
        INSERT INTO "TB_SALDO_CONSOLIDADO_DIARIO"
        (
            "DATA_REFERENCIA",
            "TOTAL_CREDITOS",
            "TOTAL_DEBITOS",
            "SALDO_DIARIO_CONSOLIDADO",
            "DATA_ATUALIZACAO"
        )
        SELECT
            @DataReferencia,
            CASE WHEN @Tipo = 'C' THEN @Valor ELSE 0 END,
            CASE WHEN @Tipo = 'D' THEN @Valor ELSE 0 END,
            CASE WHEN @Tipo = 'C' THEN @Valor ELSE -@Valor END,
            @DataProcessamento
        FROM historico_inserido
        ON CONFLICT ("DATA_REFERENCIA") DO UPDATE
        SET
            "TOTAL_CREDITOS" = "TB_SALDO_CONSOLIDADO_DIARIO"."TOTAL_CREDITOS"
                + EXCLUDED."TOTAL_CREDITOS",
            "TOTAL_DEBITOS" = "TB_SALDO_CONSOLIDADO_DIARIO"."TOTAL_DEBITOS"
                + EXCLUDED."TOTAL_DEBITOS",
            "SALDO_DIARIO_CONSOLIDADO" =
                ("TB_SALDO_CONSOLIDADO_DIARIO"."TOTAL_CREDITOS" + EXCLUDED."TOTAL_CREDITOS")
                - ("TB_SALDO_CONSOLIDADO_DIARIO"."TOTAL_DEBITOS" + EXCLUDED."TOTAL_DEBITOS"),
            "DATA_ATUALIZACAO" = EXCLUDED."DATA_ATUALIZACAO";
        """;


    public async Task<SaldoConsolidado?> ObterPorDataAsync(DateOnly dataReferencia, CancellationToken cancellationToken)
    {
        await using var conexao = await dataSource.OpenConnectionAsync(cancellationToken);

        return await conexao.QuerySingleOrDefaultAsync<SaldoConsolidado>(
            new CommandDefinition(
                SqlObterPorData,
                new
                {
                    DataReferencia = dataReferencia.ToDateTime(TimeOnly.MinValue)
                },
                cancellationToken: cancellationToken));
    }

    public async Task<bool> ProcessarLancamentoAsync(
        HistoricoLancamento historicoLancamento,
        DateOnly dataReferencia,
        CancellationToken cancellationToken
        )
    {
        ArgumentNullException.ThrowIfNull(historicoLancamento);

        await using var conexao = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transacao = await conexao.BeginTransactionAsync(cancellationToken);

        var parametros = new
        {
            historicoLancamento.CorrelationId,
            Tipo = ConverterTipo(historicoLancamento.Tipo),
            historicoLancamento.DataLancamento,
            historicoLancamento.Valor,
            historicoLancamento.Descricao,
            historicoLancamento.DataProcessamento,
            DataReferencia = dataReferencia.ToDateTime(TimeOnly.MinValue)
        };

        var linhasAfetadas = await conexao.ExecuteAsync(
            new CommandDefinition(
                SqlProcessarLancamento,
                parametros,
                transaction: transacao,
                cancellationToken: cancellationToken)
            );

        await transacao.CommitAsync(cancellationToken);

        return linhasAfetadas > 0;
    }

    private static string ConverterTipo(TipoDeLancamento tipo)
    {
        return tipo switch
        {
            TipoDeLancamento.Debito => "D",
            TipoDeLancamento.Credito => "C",
            _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Tipo de lançamento inválido.")
        };
    }
}
