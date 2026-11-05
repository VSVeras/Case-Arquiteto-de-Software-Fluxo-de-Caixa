using FluentAssertions;
using LivroRazao.Infraestrutura.Persistencia.Entidade.Outbox;
using Xunit;

namespace LivroRazao.TesteDeUnidade.Infraestrutura;

public sealed class LancamentoOutboxTestes
{
    [Fact]
    public void DeveIniciarPendente()
    {
        var outbox = CriarOutbox();

        outbox.Status.Should().Be(StatusDaOutbox.Pendente);
        outbox.Tentativas.Should().Be(0);
        outbox.DataPublicacao.Should().BeNull();
        outbox.DataUltimaTentativa.Should().BeNull();
        outbox.UltimoErro.Should().BeNull();
    }

    [Fact]
    public void DeveLimitarMensagemDeErroA2000Caracteres()
    {
        var outbox = CriarOutbox();
        outbox.ReservarParaProcessamento();

        outbox.RegistrarFalha(new string('A', 2001));

        outbox.UltimoErro.Should().HaveLength(2000);
    }

    [Fact]
    public void DeveLimparUltimoErroAoPublicar()
    {
        var outbox = CriarOutbox();
        outbox.ReservarParaProcessamento();
        outbox.RegistrarFalha("erro");
        outbox.ReservarParaProcessamento();

        outbox.MarcarComoPublicado();

        outbox.UltimoErro.Should().BeNull();
    }

    [Fact]
    public void DeveManterPendenteAposFalhaDePublicacao()
    {
        var outbox = CriarOutbox();
        outbox.ReservarParaProcessamento();

        outbox.RegistrarFalha("erro");

        outbox.Status.Should().Be(StatusDaOutbox.Pendente);
    }


    [Fact]
    public void DeveContinuarPendenteMesmoAposVariasFalhasDePublicacao()
    {
        var outbox = CriarOutbox();

        for (var tentativa = 0; tentativa < 3; tentativa++)
        {
            outbox.ReservarParaProcessamento();
            outbox.RegistrarFalha("erro");
        }

        outbox.Status.Should().Be(StatusDaOutbox.Pendente);
        outbox.Tentativas.Should().Be(3);
        outbox.UltimoErro.Should().Be("erro");
    }

    [Fact]
    public void DeveMarcarComoPublicado()
    {
        var outbox = CriarOutbox();
        outbox.ReservarParaProcessamento();

        outbox.MarcarComoPublicado();

        outbox.Status.Should().Be(StatusDaOutbox.Publicado);
        outbox.DataPublicacao.Should().NotBeNull();
    }

    [Fact]
    public void DevePermitirRecuperarEventoEmProcessamento()
    {
        var outbox = CriarOutbox();
        outbox.ReservarParaProcessamento();

        var acao = () => outbox.ReservarParaProcessamento();

        acao.Should().NotThrow();
        outbox.Status.Should().Be(StatusDaOutbox.Processando);
    }

    [Fact]
    public void DeveReservarEventoPendente()
    {
        var outbox = CriarOutbox();

        outbox.ReservarParaProcessamento();

        outbox.Status.Should().Be(StatusDaOutbox.Processando);
        outbox.DataUltimaTentativa.Should().NotBeNull();
    }

    [Fact]
    public void DeveRetornarParaPendenteAposFalha()
    {
        var outbox = CriarOutbox();
        outbox.ReservarParaProcessamento();

        outbox.RegistrarFalha("erro");

        outbox.Status.Should().Be(StatusDaOutbox.Pendente);
        outbox.Tentativas.Should().Be(1);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DeveUsarMensagemPadraoQuandoErroForVazio(string? erro)
    {
        var outbox = CriarOutbox();
        outbox.ReservarParaProcessamento();

        outbox.RegistrarFalha(erro!);

        outbox.UltimoErro.Should().Be("Erro não informado.");
    }

    [Fact]
    public void NaoDevePublicarEventoQueNaoEstaProcessando()
    {
        var outbox = CriarOutbox();

        var acao = () => outbox.MarcarComoPublicado();

        acao.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("Somente eventos em processamento podem ser publicados.");
    }
    [Fact]
    public void NaoDeveRegistrarFalhaQuandoNaoEstiverEmProcessamento()
    {
        var outbox = CriarOutbox();

        var acao = () => outbox.RegistrarFalha("erro");

        acao.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("Somente eventos em processamento podem registrar falha.");
    }

    [Fact]
    public void DevePermitirNovaReservaDepoisDeFalha()
    {
        var outbox = CriarOutbox();
        outbox.ReservarParaProcessamento();
        outbox.RegistrarFalha("erro");

        var acao = () => outbox.ReservarParaProcessamento();

        acao.Should().NotThrow();
        outbox.Status.Should().Be(StatusDaOutbox.Processando);
    }

    [Fact]
    public void NaoDeveReservarEventoPublicado()
    {
        var outbox = CriarOutbox();
        outbox.ReservarParaProcessamento();
        outbox.MarcarComoPublicado();

        var acao = () => outbox.ReservarParaProcessamento();

        acao.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("O evento não pode ser reservado para processamento.");
    }

    private static LancamentoOutbox CriarOutbox()
    {
        return new LancamentoOutbox(
            Guid.NewGuid(),
            "LancamentoCriado",
            "{}",
            DateTimeOffset.UtcNow
            );
    }
}
