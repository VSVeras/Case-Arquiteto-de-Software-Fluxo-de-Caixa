using LivroRazao.Dominio.Caixa;

namespace LivroRazao.Aplicacao.Dto;

public sealed record CriarLancamentoRequisicao(
    Guid CorrelationId,
    TipoDeLancamento Tipo,
    DateTimeOffset DataLancamento,
    decimal Valor,
    string? Descricao
    );
