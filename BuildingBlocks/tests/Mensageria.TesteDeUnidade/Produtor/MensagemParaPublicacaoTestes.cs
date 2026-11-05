using FluentAssertions; using Mensageria.Produtor; using Xunit;
namespace Mensageria.TesteDeUnidade.Produtor;
public sealed class MensagemParaPublicacaoTestes
{
 [Fact] public void DeveAceitarMensagemValida() => new MensagemParaPublicacao(Guid.NewGuid(), "Evento", "{}").Invoking(x => x.Validar()).Should().NotThrow();
 [Fact] public void DeveRejeitarCorrelationIdVazio() => new MensagemParaPublicacao(Guid.Empty, "Evento", "{}").Invoking(x => x.Validar()).Should().Throw<ArgumentException>();
 [Fact] public void DeveRejeitarTipoVazio() => new MensagemParaPublicacao(Guid.NewGuid(), "", "{}").Invoking(x => x.Validar()).Should().Throw<ArgumentException>();
 [Fact] public void DeveRejeitarConteudoVazio() => new MensagemParaPublicacao(Guid.NewGuid(), "Evento", "").Invoking(x => x.Validar()).Should().Throw<ArgumentException>();
}
