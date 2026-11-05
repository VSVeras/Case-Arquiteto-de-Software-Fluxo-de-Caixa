using FluentAssertions; using Mensageria.RabbitMQ.Configuracao; using Xunit;
namespace Mensageria.TesteDeUnidade.RabbitMQ;
public sealed class RabbitMqConfiguracaoTestes
{
 [Fact] public void DevePreservarTopologiaDoFluxoDeCaixa()
 {
  var c = new RabbitMqConfiguracao();
  c.Exchange.Should().Be("fluxocaixa.eventos"); c.Fila.Should().Be("fluxocaixa.lancamento.criado");
  c.RetryExchange.Should().Be("fluxocaixa.eventos.retry"); c.RetryFila.Should().Be("fluxocaixa.lancamento.criado.retry"); c.RetryTempoEmMilissegundos.Should().Be(30000);
  c.DeadLetterExchange.Should().Be("fluxocaixa.eventos.dlx"); c.DeadLetterFila.Should().Be("fluxocaixa.lancamento.criado.dlq"); c.QuantidadeMaximaDeTentativas.Should().Be(3);
 }
}
