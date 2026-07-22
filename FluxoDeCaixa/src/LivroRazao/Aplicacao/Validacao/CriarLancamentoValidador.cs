using FluentValidation;
using LivroRazao.Aplicacao.Dto;
using LivroRazao.Dominio.Caixa;

namespace LivroRazao.Aplicacao.Validacao;

public sealed class CriarLancamentoValidador
    : AbstractValidator<CriarLancamentoRequisicao>
{
    public CriarLancamentoValidador()
    {
        RuleFor(x => x.CorrelationId)
            .NotEmpty()
            .WithMessage("O correlationId deve ser informado.");

        RuleFor(x => x.Tipo)
            .Must(tipo =>
                tipo is TipoDeLancamento.Debito
                    or TipoDeLancamento.Credito)
            .WithMessage("O tipo deve ser 0 para débito ou 1 para crédito.");

        RuleFor(x => x.DataLancamento)
            .NotEmpty()
            .WithMessage("A dataLancamento deve ser informada.");

        RuleFor(x => x.Valor)
            .GreaterThan(0)
            .WithMessage("O valor deve ser maior que zero.");

        RuleFor(x => x.Descricao)
            .MaximumLength(100)
            .WithMessage("A descrição deve possuir no máximo 100 caracteres.");
    }
}
