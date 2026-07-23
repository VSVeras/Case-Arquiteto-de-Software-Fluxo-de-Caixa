using ConsolidadoDiario.Infraestrutura.Mensageria;
using FluentAssertions;
using Xunit;

namespace ConsolidadoDiario.TesteDeUnidade.Infraestrutura.Mensageria;

public sealed class RabbitMqConfiguracaoTestes
{
    [Fact]
    public void DevePossuirValoresPadraoValidos()
    {
        var configuracao =
            new RabbitMqConfiguracao();

        configuracao.Host
            .Should()
            .Be("localhost");

        configuracao.Porta
            .Should()
            .Be(5672);

        configuracao.Usuario
            .Should()
            .Be("guest");

        configuracao.Senha
            .Should()
            .Be("guest");

        configuracao.VirtualHost
            .Should()
            .Be("/");

        configuracao.Exchange
            .Should()
            .Be("fluxocaixa.eventos");

        configuracao.RoutingKeyLancamentoCriado
            .Should()
            .Be("lancamento.criado");

        configuracao.FilaLancamentoCriado
            .Should()
            .Be("fluxocaixa.lancamento.criado");

        configuracao
            .QuantidadeDeMensagensEmProcessamento
            .Should()
            .Be(1);

        RabbitMqConfiguracao.Secao
            .Should()
            .Be("RabbitMq");
    }
}
