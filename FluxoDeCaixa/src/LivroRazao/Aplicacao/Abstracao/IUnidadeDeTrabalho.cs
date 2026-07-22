namespace LivroRazao.Aplicacao.Abstracao;

public interface IUnidadeDeTrabalho
{
    Task ExecutarEmTransacaoAsync(Func<CancellationToken, Task> operacao, CancellationToken cancellationToken);
    Task SalvarAlteracoesAsync(CancellationToken cancellationToken);
    void LimparRastreamento();
}
