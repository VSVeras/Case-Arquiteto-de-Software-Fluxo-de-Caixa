using FluentAssertions;
using LivroRazao.Aplicacao.Abstracao;
using Mensageria.Produtor;
using LivroRazao.Infraestrutura.Persistencia.Entidade.Outbox;
using LivroRazao.Infraestrutura.Worker;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace LivroRazao.TesteDeUnidade.Infraestrutura;

public sealed class ProcessadorOutboxTestes
{
    [Fact]
    public async Task CancelamentoDeveEncerrarSemRegistrarErro()
    {
        var cenario = new Cenario();
        cenario.ConfigurarEvento();

        using var cancelamento =
            new CancellationTokenSource();

        cancelamento.Cancel();

        cenario.Publicador
            .Setup(x => x.PublicarAsync(
                It.IsAny<MensagemParaPublicacao>(),
                It.IsAny<CancellationToken>()
                ))
            .ThrowsAsync(
                new OperationCanceledException(
                    cancelamento.Token
                    )
                );

        Func<Task> acao = async () =>
            await cenario.Processador.ProcessarLoteAsync(
                cancelamento.Token
                );

        await acao.Should()
            .ThrowAsync<OperationCanceledException>();

        cenario.RegistroDeEvento.Verify(
            x => x.Aviso(
                It.IsAny<Exception>(),
                It.IsAny<string>(),
                It.IsAny<object?[]>()
                ),
            Times.Never
            );

        cenario.UnidadeDeTrabalho.Verify(
            x => x.SalvarAlteracoesAsync(
                It.IsAny<CancellationToken>()
                ),
            Times.Never
            );
    }

    [Fact]
    public async Task EventoPublicadoDeveMarcarComoPublicado()
    {
        var cenario = new Cenario();
        var evento = cenario.ConfigurarEvento();

        var resultado = await cenario.Processador.ProcessarLoteAsync(
            CancellationToken.None
            );

        resultado.Should().BeTrue();
        evento.Status.Should().Be(StatusDaOutbox.Publicado);

        cenario.Publicador.Verify(
            x => x.PublicarAsync(
                It.Is<MensagemParaPublicacao>(mensagem =>
                    mensagem.Tipo == evento.TipoEvento
                    && mensagem.Conteudo == evento.Payload
                    && mensagem.CorrelationId == evento.CorrelationId),
                It.IsAny<CancellationToken>()
                ),
            Times.Once
            );

        cenario.UnidadeDeTrabalho.Verify(
            x => x.SalvarAlteracoesAsync(
                It.IsAny<CancellationToken>()
                ),
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
                It.IsAny<MensagemParaPublicacao>(),
                It.IsAny<CancellationToken>()
                ))
            .ThrowsAsync(
                new InvalidOperationException("Falha.")
                );

        var resultado = await cenario.Processador.ProcessarLoteAsync(
            CancellationToken.None
            );

        resultado.Should().BeTrue();
        evento.Status.Should().Be(StatusDaOutbox.Pendente);
        evento.Tentativas.Should().Be(1);
        evento.UltimoErro.Should().Be("Falha.");

        cenario.UnidadeDeTrabalho.Verify(
            x => x.SalvarAlteracoesAsync(
                It.IsAny<CancellationToken>()
                ),
            Times.Once
            );
    }

    [Fact]
    public async Task FalhaAoSalvarLoteDevePropagarExcecao()
    {
        var cenario = new Cenario();
        cenario.ConfigurarEvento();

        cenario.UnidadeDeTrabalho
            .Setup(x => x.SalvarAlteracoesAsync(
                It.IsAny<CancellationToken>()
                ))
            .ThrowsAsync(
                new InvalidOperationException(
                    "Falha ao salvar o lote."
                    )
                );

        Func<Task> acao = async () =>
            await cenario.Processador.ProcessarLoteAsync(
                CancellationToken.None
                );

        await acao.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Falha ao salvar o lote.");
    }

    [Fact]
    public async Task FalhaEmUmEventoNaoDeveImpedirProcessamentoDoProximo()
    {
        var cenario = new Cenario();

        var primeiroEvento = cenario.CriarEvento();
        var segundoEvento = cenario.CriarEvento();

        cenario.ConfigurarEventos(
            primeiroEvento,
            segundoEvento
            );

        cenario.Publicador
            .Setup(x => x.PublicarAsync(
                It.Is<MensagemParaPublicacao>(mensagem =>
                    mensagem.Tipo == primeiroEvento.TipoEvento
                    && mensagem.Conteudo == primeiroEvento.Payload
                    && mensagem.CorrelationId == primeiroEvento.CorrelationId),
                It.IsAny<CancellationToken>()
                ))
            .ThrowsAsync(
                new InvalidOperationException(
                    "Falha no primeiro evento."
                    )
                );

        cenario.Publicador
            .Setup(x => x.PublicarAsync(
                It.Is<MensagemParaPublicacao>(mensagem =>
                    mensagem.Tipo == segundoEvento.TipoEvento
                    && mensagem.Conteudo == segundoEvento.Payload
                    && mensagem.CorrelationId == segundoEvento.CorrelationId),
                It.IsAny<CancellationToken>()
                ))
            .Returns(Task.CompletedTask);

        var resultado = await cenario.Processador.ProcessarLoteAsync(
            CancellationToken.None
            );

        resultado.Should().BeTrue();

        primeiroEvento.Status.Should().Be(
            StatusDaOutbox.Pendente
            );

        primeiroEvento.Tentativas.Should().Be(1);

        segundoEvento.Status.Should().Be(
            StatusDaOutbox.Publicado
            );

        cenario.Publicador.Verify(
            x => x.PublicarAsync(
                It.IsAny<MensagemParaPublicacao>(),
                It.IsAny<CancellationToken>()
                ),
            Times.Exactly(2)
            );

        cenario.UnidadeDeTrabalho.Verify(
            x => x.SalvarAlteracoesAsync(
                It.IsAny<CancellationToken>()
                ),
            Times.Once
            );
    }

    [Fact]
    public async Task FalhaNoLimiteDeveManterEventoPendente()
    {
        var cenario = new Cenario(
            intervaloDeAlertaDeTentativas: 1
            );

        var evento = cenario.ConfigurarEvento();

        cenario.Publicador
            .Setup(x => x.PublicarAsync(
                It.IsAny<MensagemParaPublicacao>(),
                It.IsAny<CancellationToken>()
                ))
            .ThrowsAsync(
                new InvalidOperationException("Falha.")
                );

        var resultado = await cenario.Processador.ProcessarLoteAsync(
            CancellationToken.None
            );

        resultado.Should().BeTrue();
        evento.Status.Should().Be(StatusDaOutbox.Pendente);
        evento.Tentativas.Should().Be(1);
        evento.UltimoErro.Should().Be("Falha.");

        cenario.RegistroDeEvento.Verify(
            x => x.Erro(
                It.IsAny<Exception?>(),
                It.Is<string>(mensagem =>
                    mensagem.Contains("permanecera pendente")),
                It.IsAny<object?[]>()
                ),
            Times.Once
            );

        cenario.UnidadeDeTrabalho.Verify(
            x => x.SalvarAlteracoesAsync(
                It.IsAny<CancellationToken>()
                ),
            Times.Once
            );
    }

    [Fact]
    public async Task LoteComDoisEventosDevePublicarTodosESalvarUmaVez()
    {
        var cenario = new Cenario();

        var primeiroEvento = cenario.CriarEvento();
        var segundoEvento = cenario.CriarEvento();

        cenario.ConfigurarEventos(
            primeiroEvento,
            segundoEvento
            );

        var resultado = await cenario.Processador.ProcessarLoteAsync(
            CancellationToken.None
            );

        resultado.Should().BeTrue();

        primeiroEvento.Status.Should().Be(
            StatusDaOutbox.Publicado
            );

        segundoEvento.Status.Should().Be(
            StatusDaOutbox.Publicado
            );

        cenario.Publicador.Verify(
            x => x.PublicarAsync(
                It.IsAny<MensagemParaPublicacao>(),
                It.IsAny<CancellationToken>()
                ),
            Times.Exactly(2)
            );

        cenario.UnidadeDeTrabalho.Verify(
            x => x.SalvarAlteracoesAsync(
                It.IsAny<CancellationToken>()
                ),
            Times.Once
            );
    }

    [Fact]
    public async Task SemEventoDisponivelDeveRetornarFalse()
    {
        var cenario = new Cenario();

        cenario.Repositorio
            .Setup(x => x.ReservarLoteAsync(
                1,
                5,
                100,
                It.IsAny<CancellationToken>()
                ))
            .ReturnsAsync(Array.Empty<LancamentoOutbox>());

        var resultado = await cenario.Processador.ProcessarLoteAsync(
            CancellationToken.None
            );

        resultado.Should().BeFalse();

        cenario.Publicador.Verify(
            x => x.PublicarAsync(
                It.IsAny<MensagemParaPublicacao>(),
                It.IsAny<CancellationToken>()
                ),
            Times.Never
            );

        cenario.UnidadeDeTrabalho.Verify(
            x => x.SalvarAlteracoesAsync(
                It.IsAny<CancellationToken>()
                ),
            Times.Never
            );
    }

    private sealed class Cenario
    {
        public Cenario(
            int intervaloDeAlertaDeTentativas = 3)
        {
            var configuracao = Options.Create(
                new PublicadorOutboxConfiguracao
                {
                    IntervaloEmSegundos = 1,
                    IntervaloDeAlertaDeTentativas = intervaloDeAlertaDeTentativas,
                    TempoLimiteDeProcessamentoEmMinutos = 5,
                    QuantidadePorLote = 100
                }
                );

            UnidadeDeTrabalho
                .Setup(x => x.SalvarAlteracoesAsync(
                    It.IsAny<CancellationToken>()
                    ))
                .Returns(Task.CompletedTask);

            Processador = new ProcessadorOutbox(
                Repositorio.Object,
                Publicador.Object,
                UnidadeDeTrabalho.Object,
                configuracao,
                RegistroDeEvento.Object
                );
        }

        public ProcessadorOutbox Processador { get; }

        public Mock<IPublicador> Publicador { get; }
            = new();

        public Mock<IRegistroDeEvento> RegistroDeEvento { get; }
            = new();

        public Mock<ILancamentoOutboxRepositorio> Repositorio { get; }
                                            = new();

        public Mock<IUnidadeDeTrabalho> UnidadeDeTrabalho { get; }
            = new();

        public LancamentoOutbox ConfigurarEvento()
        {
            var evento = CriarEvento();

            ConfigurarEventos(evento);

            return evento;
        }

        public void ConfigurarEventos(
            params LancamentoOutbox[] eventos)
        {
            Repositorio
                .Setup(x => x.ReservarLoteAsync(
                    1,
                    5,
                    100,
                    It.IsAny<CancellationToken>()
                    ))
                .ReturnsAsync(eventos);

            Publicador
                .Setup(x => x.PublicarAsync(
                It.IsAny<MensagemParaPublicacao>(),
                It.IsAny<CancellationToken>()
                ))
                .Returns(Task.CompletedTask);
        }

        public LancamentoOutbox CriarEvento()
        {
            var evento = new LancamentoOutbox(
                Guid.NewGuid(),
                "EventoDeLancamentoCriado",
                "{}",
                DateTimeOffset.UtcNow
                );

            evento.ReservarParaProcessamento();

            return evento;
        }
    }
}
