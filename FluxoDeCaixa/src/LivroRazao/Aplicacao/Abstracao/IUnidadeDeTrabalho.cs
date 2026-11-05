namespace LivroRazao.Aplicacao.Abstracao;

public interface IUnidadeDeTrabalho
{
    Task ExecutarEmTransacaoAsync(Func<CancellationToken, Task> operacao, CancellationToken cancellationToken);

    void LimparRastreamento();

    Task SalvarAlteracoesAsync(CancellationToken cancellationToken);
}
