using System.Collections; using FluentAssertions; using Mensageria.RabbitMQ.Retry; using Xunit;
namespace Mensageria.TesteDeUnidade.RabbitMQ;
public sealed class ContadorDeTentativasRabbitMqTestes
{
 [Fact] public void DeveRetornarZeroSemXDeath() => ContadorDeTentativasRabbitMq.Obter(new Dictionary<string, object?>()).Should().Be(0);
 [Fact] public void DeveObterMaiorQuantidadeDoXDeath()
 {
  IList xdeath = new List<object> { new Dictionary<string, object?> { ["count"] = 1L }, new Dictionary<string, object?> { ["count"] = 3L } };
  ContadorDeTentativasRabbitMq.Obter(new Dictionary<string, object?> { ["x-death"] = xdeath }).Should().Be(3);
 }
}
