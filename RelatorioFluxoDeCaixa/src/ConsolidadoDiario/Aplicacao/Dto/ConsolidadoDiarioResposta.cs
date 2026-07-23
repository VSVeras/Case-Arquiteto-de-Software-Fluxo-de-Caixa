namespace ConsolidadoDiario.Aplicacao.Dto;

public sealed record ConsolidadoDiarioResposta(
    DateOnly DataReferencia,
    decimal TotalCreditos,
    decimal TotalDebitos,
    decimal SaldoDiarioConsolidado,
    DateTimeOffset DataAtualizacao
    );
