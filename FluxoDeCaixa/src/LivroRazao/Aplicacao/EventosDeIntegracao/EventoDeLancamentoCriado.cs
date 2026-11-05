namespace LivroRazao.Aplicacao.EventosDeIntegracao;

public sealed record EventoDeLancamentoCriado(
    Guid CorrelationId,
    string TipoEvento,
    DateTimeOffset DataOcorrencia,
    DadosDoLancamento Payload);
public sealed record DadosDoLancamento(
    long LancamentoId,
    string Tipo,
    DateTimeOffset DataLancamento,
    decimal Valor,
    string? Descricao);
