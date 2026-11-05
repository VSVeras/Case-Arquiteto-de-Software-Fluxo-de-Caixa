using LivroRazao.Dominio.Caixa;

namespace LivroRazao.Aplicacao.Dto;

public sealed record LancamentoResposta(
    long Id,
    Guid CorrelationId,
    TipoDeLancamento Tipo,
    DateTimeOffset DataLancamento,
    decimal Valor,
    string? Descricao,
    DateTimeOffset DataCriacao
    );
