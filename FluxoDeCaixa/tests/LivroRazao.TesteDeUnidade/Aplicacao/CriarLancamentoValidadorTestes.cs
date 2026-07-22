using FluentAssertions;
using LivroRazao.Aplicacao.Dto;
using LivroRazao.Aplicacao.Validacao;
using LivroRazao.Dominio.Caixa;
using Xunit;

namespace LivroRazao.TesteDeUnidade.Aplicacao;

public sealed class CriarLancamentoValidadorTestes
{
    private readonly CriarLancamentoValidador _validador = new();

    [Fact]
    public async Task DeveAceitarRequisicaoValida()
    {
        var resultado = await _validador.ValidateAsync(CriarRequisicaoValida());

        resultado.IsValid.Should().BeTrue();
        resultado.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task DeveRejeitarCorrelationIdVazio()
    {
        var requisicao = CriarRequisicaoValida() with
        {
            CorrelationId = Guid.Empty
        };

        var resultado = await _validador.ValidateAsync(requisicao);

        resultado.Errors.Should().ContainSingle(
            x => x.PropertyName == nameof(requisicao.CorrelationId)
            );
    }

    [Fact]
    public async Task DeveRejeitarTipoInvalido()
    {
        var requisicao = CriarRequisicaoValida() with
        {
            Tipo = (TipoDeLancamento)999
        };

        var resultado = await _validador.ValidateAsync(requisicao);

        resultado.Errors.Should().ContainSingle(
            x => x.PropertyName == nameof(requisicao.Tipo)
            );
    }

    [Fact]
    public async Task DeveRejeitarDataVazia()
    {
        var requisicao = CriarRequisicaoValida() with
        {
            DataLancamento = default
        };

        var resultado = await _validador.ValidateAsync(requisicao);

        resultado.Errors.Should().ContainSingle(
            x => x.PropertyName == nameof(requisicao.DataLancamento)
            );
    }

    [Fact]
    public async Task DeveRejeitarValorZero()
    {
        var requisicao = CriarRequisicaoValida() with
        {
            Valor = 0
        };

        var resultado = await _validador.ValidateAsync(requisicao);

        resultado.Errors.Should().ContainSingle(
            x => x.PropertyName == nameof(requisicao.Valor)
            );
    }

    [Fact]
    public async Task DeveRejeitarValorNegativo()
    {
        var requisicao = CriarRequisicaoValida() with
        {
            Valor = -1
        };

        var resultado = await _validador.ValidateAsync(requisicao);

        resultado.Errors.Should().ContainSingle(
            x => x.PropertyName == nameof(requisicao.Valor)
            );
    }

    [Fact]
    public async Task DeveRejeitarDescricaoMaiorQue100()
    {
        var requisicao = CriarRequisicaoValida() with
        {
            Descricao = new string('A', 101)
        };

        var resultado = await _validador.ValidateAsync(requisicao);

        resultado.Errors.Should().ContainSingle(
            x => x.PropertyName == nameof(requisicao.Descricao)
            );
    }

    [Fact]
    public async Task DeveAceitarDescricaoNula()
    {
        var requisicao = CriarRequisicaoValida() with
        {
            Descricao = null
        };

        var resultado = await _validador.ValidateAsync(requisicao);

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task DeveAcumularTodosOsErros()
    {
        var requisicao = new CriarLancamentoRequisicao(
            Guid.Empty,
            (TipoDeLancamento)999,
            default,
            0,
            new string('A', 101)
            );

        var resultado = await _validador.ValidateAsync(requisicao);

        resultado.Errors.Should().HaveCount(5);
    }

    private static CriarLancamentoRequisicao CriarRequisicaoValida()
    {
        return new CriarLancamentoRequisicao(
            Guid.NewGuid(),
            TipoDeLancamento.Credito,
            DateTimeOffset.UtcNow,
            100,
            "Teste"
            );
    }
}
