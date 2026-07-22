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
    public void DeveReservarEventoPendente()
    {
        var outbox = CriarOutbox();

        outbox.ReservarParaProcessamento();

        outbox.Status.Should().Be(StatusDaOutbox.Processando);
        outbox.DataUltimaTentativa.Should().NotBeNull();
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

    [Fact]
    public void NaoDeveReservarEventoComErro()
    {
        var outbox = CriarOutbox();
        outbox.ReservarParaProcessamento();
        outbox.RegistrarFalha("erro", 1);

        var acao = () => outbox.ReservarParaProcessamento();

        acao.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("O evento não pode ser reservado para processamento.");
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
    public void NaoDevePublicarEventoQueNaoEstaProcessando()
    {
        var outbox = CriarOutbox();

        var acao = () => outbox.MarcarComoPublicado();

        acao.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("Somente eventos em processamento podem ser publicados.");
    }

    [Fact]
    public void DeveLimparUltimoErroAoPublicar()
    {
        var outbox = CriarOutbox();
        outbox.ReservarParaProcessamento();
        outbox.RegistrarFalha("erro", 2);
        outbox.ReservarParaProcessamento();

        outbox.MarcarComoPublicado();

        outbox.UltimoErro.Should().BeNull();
    }

    [Fact]
    public void DeveRetornarParaPendenteAbaixoDoLimite()
    {
        var outbox = CriarOutbox();
        outbox.ReservarParaProcessamento();

        outbox.RegistrarFalha("erro", 2);

        outbox.Status.Should().Be(StatusDaOutbox.Pendente);
        outbox.Tentativas.Should().Be(1);
    }

    [Fact]
    public void DeveMarcarComoErroAoAtingirLimiteDeTentativas()
    {
        var outbox = CriarOutbox();
        outbox.ReservarParaProcessamento();

        outbox.RegistrarFalha("erro", 1);

        outbox.Status.Should().Be(StatusDaOutbox.Erro);
    }

    [Fact]
    public void DeveLimitarMensagemDeErroA2000Caracteres()
    {
        var outbox = CriarOutbox();
        outbox.ReservarParaProcessamento();

        outbox.RegistrarFalha(new string('A', 2001), 2);

        outbox.UltimoErro.Should().HaveLength(2000);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DeveUsarMensagemPadraoQuandoErroForVazio(string? erro)
    {
        var outbox = CriarOutbox();
        outbox.ReservarParaProcessamento();

        outbox.RegistrarFalha(erro!, 2);

        outbox.UltimoErro.Should().Be("Erro não informado.");
    }

    [Fact]
    public void NaoDeveRegistrarFalhaQuandoNaoEstiverEmProcessamento()
    {
        var outbox = CriarOutbox();

        var acao = () => outbox.RegistrarFalha("erro", 3);

        acao.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("Somente eventos em processamento podem registrar falha.");
    }

    [Fact]
    public void NaoDeveRegistrarFalhaQuandoLimiteDeTentativasForInvalido()
    {
        var outbox = CriarOutbox();
        outbox.ReservarParaProcessamento();

        var acao = () => outbox.RegistrarFalha("erro", 0);

        acao.Should()
            .Throw<ArgumentOutOfRangeException>();
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
