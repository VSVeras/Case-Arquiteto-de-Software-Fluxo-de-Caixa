namespace ConsolidadoDiario.Infraestrutura.Persistencia.Entidade;

public sealed record SaldoConsolidado(
    DateTime DataReferencia,
    decimal TotalCreditos,
    decimal TotalDebitos,
    decimal SaldoDiarioConsolidado,
    DateTime DataAtualizacao
    );
