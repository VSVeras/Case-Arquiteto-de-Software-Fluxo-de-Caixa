using Seguranca.Autenticacao;
using FluentAssertions;
using Xunit;

namespace Seguranca.TesteDeUnidade.Autenticacao;

public sealed class JwtConfiguracaoTestes
{
    [Fact]
    public void DevePossuirSecaoEsperada()
    {
        JwtConfiguracao.Secao.Should().Be("Jwt");
    }

    [Fact]
    public void DevePossuirValoresPadraoVazios()
    {
        var configuracao = new JwtConfiguracao();

        configuracao.Issuer.Should().BeEmpty();
        configuracao.Audience.Should().BeEmpty();
        configuracao.Chave.Should().BeEmpty();
    }
}
