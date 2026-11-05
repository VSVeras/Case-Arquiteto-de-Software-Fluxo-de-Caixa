using FluentAssertions;
using LivroRazao.Dominio;
using LivroRazao.Dominio.Caixa;
using Xunit;

namespace LivroRazao.TesteDeUnidade.Dominio;

public sealed class LancamentoTestes
{
    [Fact]
    public void DeveConsiderarMesmaOperacaoComDescricaoNormalizada()
    {
        var dataLancamento = DateTimeOffset.UtcNow;
        var lancamento = new Lancamento(
            Guid.NewGuid(),
            TipoDeLancamento.Credito,
            dataLancamento,
            100,
            " Teste "
            );

        var resultado = lancamento.RepresentaMesmaOperacao(
            TipoDeLancamento.Credito,
            dataLancamento,
            100,
            "Teste"
            );

        resultado.Should().BeTrue();
    }

    [Fact]
    public void DeveNormalizarDataLancamentoParaUtc()
    {
        var dataComOffset = new DateTimeOffset(
            2026, 7, 23, 15, 0, 0, TimeSpan.FromHours(-3));

        var lancamento = CriarLancamento(dataComOffset);

        lancamento.DataLancamento.Should().Be(
            new DateTimeOffset(2026, 7, 23, 18, 0, 0, TimeSpan.Zero));
        lancamento.DataLancamento.Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void DeveConsiderarMesmaOperacaoQuandoDatasRepresentamMesmoInstante()
    {
        var dataComOffset = new DateTimeOffset(
            2026, 7, 23, 15, 0, 0, TimeSpan.FromHours(-3));

        var mesmaDataEmUtc = new DateTimeOffset(
            2026, 7, 23, 18, 0, 0, TimeSpan.Zero);

        var lancamento = CriarLancamento(dataComOffset);

        lancamento.RepresentaMesmaOperacao(
            TipoDeLancamento.Credito,
            mesmaDataEmUtc,
            100,
            "Teste"
            ).Should().BeTrue();
    }

    [Fact]
    public void DeveNormalizarDescricao()
    {
        Lancamento.NormalizarDescricao(" Teste ").Should().Be("Teste");
    }

    [Fact]
    public void DeveRetornarTodasAsViolacoes()
    {
        var acao = () =>
            new Lancamento(
                Guid.Empty,
                (TipoDeLancamento)999,
                default,
                0,
                new string('A', 101));

        var excecao =
            acao.Should()
                .Throw<ExcecaoDeDominio>()
                .Which;

        excecao.Erros.Should().BeEquivalentTo(
            [
            "O correlationId deve ser informado.",
            "O tipo de lançamento é inválido.",
            "A data do lançamento deve ser informada.",
            "O valor deve ser maior que zero.",
            "A descrição deve possuir no máximo 100 caracteres."
            ]
            );
    }

    [Fact]
    public void DeveSerValido()
    {
        var acao = () =>
            new Lancamento(
                Guid.NewGuid(),
                TipoDeLancamento.Credito,
                DateTimeOffset.UtcNow,
                100,
                "Credito");

        acao.Should().NotThrow();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DeveTransformarDescricaoVaziaEmNull(string? descricao)
    {
        Lancamento.NormalizarDescricao(descricao).Should().BeNull();
    }

    [Fact]
    public void NaoDeveConsiderarMesmaOperacaoQuandoDataForDiferente()
    {
        var dataLancamento = DateTimeOffset.UtcNow;
        var lancamento = CriarLancamento(dataLancamento);

        lancamento.RepresentaMesmaOperacao(
            TipoDeLancamento.Credito,
            dataLancamento.AddDays(1),
            100,
            "Teste"
            ).Should().BeFalse();
    }

    [Fact]
    public void NaoDeveConsiderarMesmaOperacaoQuandoDescricaoForDiferente()
    {
        var dataLancamento = DateTimeOffset.UtcNow;
        var lancamento = CriarLancamento(dataLancamento);

        lancamento.RepresentaMesmaOperacao(
            TipoDeLancamento.Credito,
            dataLancamento,
            100,
            "Outro"
            ).Should().BeFalse();
    }

    [Fact]
    public void NaoDeveConsiderarMesmaOperacaoQuandoTipoForDiferente()
    {
        var dataLancamento = DateTimeOffset.UtcNow;
        var lancamento = CriarLancamento(dataLancamento);

        lancamento.RepresentaMesmaOperacao(
            TipoDeLancamento.Debito,
            dataLancamento,
            100,
            "Teste"
            ).Should().BeFalse();
    }

    [Fact]
    public void NaoDeveConsiderarMesmaOperacaoQuandoValorForDiferente()
    {
        var dataLancamento = DateTimeOffset.UtcNow;
        var lancamento = CriarLancamento(dataLancamento);

        lancamento.RepresentaMesmaOperacao(
            TipoDeLancamento.Credito,
            dataLancamento,
            200,
            "Teste"
            ).Should().BeFalse();
    }

    private static Lancamento CriarLancamento(DateTimeOffset dataLancamento)
    {
        return new Lancamento(
            Guid.NewGuid(),
            TipoDeLancamento.Credito,
            dataLancamento,
            100,
            "Teste"
            );
    }
}
