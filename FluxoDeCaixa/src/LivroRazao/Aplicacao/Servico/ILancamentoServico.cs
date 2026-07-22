using LivroRazao.Aplicacao.Dto;

namespace LivroRazao.Aplicacao.Servico;

public interface ILancamentoServico
{
    Task<CriarLancamentoResultado> CriarAsync(CriarLancamentoRequisicao requisicao, CancellationToken cancellationToken);
    Task<LancamentoResposta?> ObterPorIdAsync(long id, CancellationToken cancellationToken);
}
