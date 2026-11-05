using FluentAssertions; using Mensageria.Assinante; using Xunit;
namespace Mensageria.TesteDeUnidade.Assinante;
public sealed class ResultadoDoProcessamentoTestes
{
 [Fact] public void DeveCriarSucesso() => ResultadoDoProcessamento.Sucesso().Tipo.Should().Be(TipoResultadoProcessamento.Sucesso);
 [Fact] public void DeveCriarErroTransitorio() => ResultadoDoProcessamento.ErroTransitorio().Tipo.Should().Be(TipoResultadoProcessamento.ErroTransitorio);
 [Fact] public void DeveCriarErroDefinitivo() => ResultadoDoProcessamento.ErroDefinitivo().Tipo.Should().Be(TipoResultadoProcessamento.ErroDefinitivo);
}
