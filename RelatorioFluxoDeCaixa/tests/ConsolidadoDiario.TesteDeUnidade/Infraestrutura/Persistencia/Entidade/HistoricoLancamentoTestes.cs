using ConsolidadoDiario.Infraestrutura.Persistencia.Entidade;
using FluentAssertions;
using Xunit;

namespace ConsolidadoDiario.TesteDeUnidade.Infraestrutura.Persistencia.Entidade;

public sealed class HistoricoLancamentoTestes
{
    [Fact]
    public void DeveCriarHistoricoDeLancamentoValido()
    {
        var correlationId = Guid.NewGuid();
        var dataLancamento = new DateTimeOffset(2026, 7, 22, 14, 30, 0, TimeSpan.Zero);
        var dataProcessamento = new DateTimeOffset(2026, 7, 22, 14, 31, 0, TimeSpan.Zero);

        var historico = new HistoricoLancamento(
            correlationId,
            TipoDeLancamento.Credito,
            dataLancamento,
            100m,
            "  Pagamento recebido  ",
            dataProcessamento);

        historico.CorrelationId.Should().Be(correlationId);
        historico.Tipo.Should().Be(TipoDeLancamento.Credito);
        historico.DataLancamento.Should().Be(dataLancamento);
        historico.Valor.Should().Be(100m);
        historico.Descricao.Should().Be("Pagamento recebido");
        historico.DataProcessamento.Should().Be(dataProcessamento);
    }

    [Fact]
    public void DeveNormalizarDescricaoEmBrancoParaNulo()
    {
        var historico = CriarHistorico(descricao: "   ");

        historico.Descricao.Should().BeNull();
    }

    [Fact]
    public void DeveRejeitarCorrelationIdVazio()
    {
        var acao = () => CriarHistorico(correlationId: Guid.Empty);

        acao.Should()
            .Throw<ArgumentException>()
            .WithParameterName("correlationId");
    }

    [Fact]
    public void DeveRejeitarDataDeLancamentoNaoInformada()
    {
        var acao = () => new HistoricoLancamento(
                Guid.NewGuid(),
                TipoDeLancamento.Credito,
                default,
                100m,
                "Descrição",
                new DateTimeOffset(2026, 7, 22, 14, 31, 0, TimeSpan.Zero));

        acao.Should()
            .Throw<ArgumentException>()
            .WithParameterName("dataLancamento");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void DeveRejeitarValorMenorOuIgualAZero(decimal valor)
    {
        var acao = () => CriarHistorico(valor: valor);

        acao.Should()
            .Throw<ArgumentOutOfRangeException>()
            .WithParameterName("valor");
    }

    [Fact]
    public void DeveRejeitarDescricaoComMaisDeCemCaracteres()
    {
        var acao = () => CriarHistorico(descricao: new string('A', 101));

        acao.Should()
            .Throw<ArgumentException>()
            .WithParameterName("descricao");
    }

    private static HistoricoLancamento CriarHistorico(
        Guid? correlationId = null,
        DateTimeOffset? dataLancamento = null,
        decimal valor = 100m,
        string? descricao = "Descrição")
    {
        return new HistoricoLancamento(
            correlationId ?? Guid.NewGuid(),
            TipoDeLancamento.Credito,
            dataLancamento ?? new DateTimeOffset(2026, 7, 22, 14, 30, 0, TimeSpan.Zero),
            valor,
            descricao,
            new DateTimeOffset(2026, 7, 22, 14, 31, 0, TimeSpan.Zero));
    }
}
