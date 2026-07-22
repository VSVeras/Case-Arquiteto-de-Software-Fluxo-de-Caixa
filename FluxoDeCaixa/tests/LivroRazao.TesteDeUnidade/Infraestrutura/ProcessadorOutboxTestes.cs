using FluentAssertions;
using LivroRazao.Aplicacao.Abstracao;
using LivroRazao.Infraestrutura.Mensageria;
using LivroRazao.Infraestrutura.Persistencia.Entidade.Outbox;
using LivroRazao.Infraestrutura.Worker;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace LivroRazao.TesteDeUnidade.Infraestrutura;

public sealed class ProcessadorOutboxTestes
{
    [Fact]
    public async Task SemEventoDisponivelDeveRetornarFalse()
    {
        var cenario = new Cenario();

        cenario.Repositorio
            .Setup(x => x.ReservarProximoAsync(1, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LancamentoOutbox?)null);

        var resultado = await cenario.Processador.ProcessarProximoAsync(CancellationToken.None);

        resultado.Should().BeFalse();
    }

    [Fact]
    public async Task EventoPublicadoDeveMarcarComoPublicado()
    {
        var cenario = new Cenario();
        var evento = cenario.ConfigurarEvento();

        var resultado = await cenario.Processador.ProcessarProximoAsync(CancellationToken.None);

        resultado.Should().BeTrue();
        evento.Status.Should().Be(StatusDaOutbox.Publicado);
        cenario.UnidadeDeTrabalho.Verify(
            x => x.SalvarAlteracoesAsync(It.IsAny<CancellationToken>()),
            Times.Once
            );
    }

    [Fact]
    public async Task FalhaAbaixoDoLimiteDeveVoltarParaPendente()
    {
        var cenario = new Cenario();
        var evento = cenario.ConfigurarEvento();

        cenario.Publicador
            .Setup(x => x.PublicarAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()
                ))
            .ThrowsAsync(new InvalidOperationException("Falha."));

        await cenario.Processador.ProcessarProximoAsync(CancellationToken.None);

        evento.Status.Should().Be(StatusDaOutbox.Pendente);
        evento.Tentativas.Should().Be(1);
    }

    [Fact]
    public async Task FalhaNoLimiteDeveMarcarComoErro()
    {
        var cenario = new Cenario(limiteDeTentativas: 1);
        var evento = cenario.ConfigurarEvento();

        cenario.Publicador
            .Setup(x => x.PublicarAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()
                ))
            .ThrowsAsync(new InvalidOperationException("Falha."));

        await cenario.Processador.ProcessarProximoAsync(CancellationToken.None);

        evento.Status.Should().Be(StatusDaOutbox.Erro);
    }

    [Fact]
    public async Task CancelamentoDeveEncerrarSemRegistrarErro()
    {
        var cenario = new Cenario();
        cenario.ConfigurarEvento();
        using var cancelamento = new CancellationTokenSource();
        cancelamento.Cancel();

        cenario.Publicador
            .Setup(x => x.PublicarAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()
                ))
            .ThrowsAsync(new OperationCanceledException(cancelamento.Token));

        var acao = () => cenario.Processador.ProcessarProximoAsync(cancelamento.Token);

        await acao.Should().ThrowAsync<OperationCanceledException>();

        cenario.RegistroDeEvento.Verify(
            x => x.Aviso(
                It.IsAny<Exception>(),
                It.IsAny<string>(),
                It.IsAny<object?[]>()
                ),
            Times.Never
            );
    }

    private sealed class Cenario
    {
        public Mock<ILancamentoOutboxRepositorio> Repositorio { get; } = new();
        public Mock<IPublicadorDeEventos> Publicador { get; } = new();
        public Mock<IUnidadeDeTrabalho> UnidadeDeTrabalho { get; } = new();
        public Mock<IRegistroDeEvento> RegistroDeEvento { get; } = new();
        public ProcessadorOutbox Processador { get; }

        public Cenario(int limiteDeTentativas = 3)
        {
            var configuracao = Options.Create(
                new PublicadorOutboxConfiguracao
                {
                    IntervaloEmSegundos = 1,
                    LimiteDeTentativas = limiteDeTentativas,
                    TempoLimiteDeProcessamentoEmMinutos = 5
                }
                );

            UnidadeDeTrabalho
                .Setup(x => x.SalvarAlteracoesAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            Processador = new ProcessadorOutbox(
                Repositorio.Object,
                Publicador.Object,
                UnidadeDeTrabalho.Object,
                configuracao,
                RegistroDeEvento.Object
                );
        }

        public LancamentoOutbox ConfigurarEvento()
        {
            var evento = new LancamentoOutbox(
                Guid.NewGuid(),
                "EventoDeLancamentoCriado",
                "{}",
                DateTimeOffset.UtcNow
                );

            evento.ReservarParaProcessamento();

            Repositorio
                .Setup(x => x.ReservarProximoAsync(1, 5, It.IsAny<CancellationToken>()))
                .ReturnsAsync(evento);

            Publicador
                .Setup(x => x.PublicarAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                    ))
                .Returns(Task.CompletedTask);

            return evento;
        }
    }
}
