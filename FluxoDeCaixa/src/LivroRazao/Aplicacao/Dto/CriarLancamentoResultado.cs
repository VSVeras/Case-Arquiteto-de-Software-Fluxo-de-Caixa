namespace LivroRazao.Aplicacao.Dto;

public sealed record CriarLancamentoResultado(
    LancamentoResposta Resposta,
    bool FoiCriado
    );
