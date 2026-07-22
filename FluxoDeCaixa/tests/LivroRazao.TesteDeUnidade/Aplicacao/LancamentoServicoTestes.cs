using System.Text.Json;
using FluentAssertions;
using LivroRazao.Aplicacao.Abstracao;
using LivroRazao.Aplicacao.Dto;
using LivroRazao.Aplicacao.EventosDeIntegracao;
using LivroRazao.Aplicacao.Servico;
using LivroRazao.Aplicacao.Validacao;
using LivroRazao.Dominio.Caixa;
using LivroRazao.Infraestrutura.Excecao;
using LivroRazao.Infraestrutura.Persistencia.Entidade.Outbox;
using Moq;
using Xunit;

namespace LivroRazao.TesteDeUnidade.Aplicacao;

public sealed class LancamentoServicoTestes
{
    [Fact]
    public async Task DeveCriarLancamentoEOutboxNaMesmaTransacao()
    {
        var cenario = new Cenario();

        await cenario.Servico.CriarAsync(cenario.Requisicao, CancellationToken.None);

        cenario.UnidadeDeTrabalho.Verify(
            x => x.ExecutarEmTransacaoAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()
                ),
            Times.Once
            );

        cenario.LancamentoRepositorio.Verify(
            x => x.AdicionarAsync(
                It.IsAny<Lancamento>(),
                It.IsAny<CancellationToken>()
                ),
            Times.Once
            );

        cenario.OutboxRepositorio.Verify(
            x => x.AdicionarAsync(
                It.IsAny<LancamentoOutbox>(),
                It.IsAny<CancellationToken>()
                ),
            Times.Once
            );

        cenario.UnidadeDeTrabalho.Verify(
            x => x.SalvarAlteracoesAsync(It.IsAny<CancellationToken>()),
            Times.Exactly(2)
            );
    }

    [Fact]
    public async Task DeveRetornarFoiCriadoTrueAoCriar()
    {
        var cenario = new Cenario();

        var resultado = await cenario.Servico.CriarAsync(
            cenario.Requisicao,
            CancellationToken.None
            );

        resultado.FoiCriado.Should().BeTrue();
        resultado.Resposta.CorrelationId.Should().Be(cenario.Requisicao.CorrelationId);
    }

    [Fact]
    public async Task DeveRetornarFoiCriadoFalseQuandoRequisicaoForIdempotente()
    {
        var cenario = new Cenario();
        var existente = cenario.CriarLancamentoExistente();

        cenario.LancamentoRepositorio
            .Setup(x => x.ObterPorCorrelationIdAsync(
                cenario.Requisicao.CorrelationId,
                It.IsAny<CancellationToken>()
                ))
            .ReturnsAsync(existente);

        var resultado = await cenario.Servico.CriarAsync(
            cenario.Requisicao,
            CancellationToken.None
            );

        resultado.FoiCriado.Should().BeFalse();
        resultado.Resposta.CorrelationId.Should().Be(existente.CorrelationId);

        cenario.UnidadeDeTrabalho.Verify(
            x => x.ExecutarEmTransacaoAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()
                ),
            Times.Never
            );
    }

    [Fact]
    public async Task DeveGerarConflitoQuandoCorrelationIdTiverDadosDiferentes()
    {
        var cenario = new Cenario();
        var existente = new Lancamento(
            cenario.Requisicao.CorrelationId,
            TipoDeLancamento.Debito,
            cenario.Requisicao.DataLancamento,
            cenario.Requisicao.Valor,
            cenario.Requisicao.Descricao
            );

        cenario.LancamentoRepositorio
            .Setup(x => x.ObterPorCorrelationIdAsync(
                cenario.Requisicao.CorrelationId,
                It.IsAny<CancellationToken>()
                ))
            .ReturnsAsync(existente);

        var acao = () => cenario.Servico.CriarAsync(
            cenario.Requisicao,
            CancellationToken.None
            );

        await acao.Should()
            .ThrowAsync<ConflitoDeRequisicaoException>();
    }

    [Fact]
    public async Task DeveRetornarExistenteAposConflitoConcorrente()
    {
        var cenario = new Cenario();
        var existente = cenario.CriarLancamentoExistente();

        cenario.LancamentoRepositorio
            .SetupSequence(x => x.ObterPorCorrelationIdAsync(
                cenario.Requisicao.CorrelationId,
                It.IsAny<CancellationToken>()
                ))
            .ReturnsAsync((Lancamento?)null)
            .ReturnsAsync(existente);

        cenario.UnidadeDeTrabalho
            .Setup(x => x.ExecutarEmTransacaoAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()
                ))
            .ThrowsAsync(new ConflitoDePersistenciaException("Conflito."));

        var resultado = await cenario.Servico.CriarAsync(
            cenario.Requisicao,
            CancellationToken.None
            );

        resultado.FoiCriado.Should().BeFalse();
        cenario.UnidadeDeTrabalho.Verify(x => x.LimparRastreamento(), Times.Once);
    }

    [Fact]
    public async Task DeveGerarConflitoAposConcorrenciaComDadosDiferentes()
    {
        var cenario = new Cenario();
        var existente = new Lancamento(
            cenario.Requisicao.CorrelationId,
            TipoDeLancamento.Debito,
            cenario.Requisicao.DataLancamento,
            cenario.Requisicao.Valor,
            cenario.Requisicao.Descricao
            );

        cenario.LancamentoRepositorio
            .SetupSequence(x => x.ObterPorCorrelationIdAsync(
                cenario.Requisicao.CorrelationId,
                It.IsAny<CancellationToken>()
                ))
            .ReturnsAsync((Lancamento?)null)
            .ReturnsAsync(existente);

        cenario.UnidadeDeTrabalho
            .Setup(x => x.ExecutarEmTransacaoAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()
                ))
            .ThrowsAsync(new ConflitoDePersistenciaException("Conflito."));

        var acao = () => cenario.Servico.CriarAsync(
            cenario.Requisicao,
            CancellationToken.None
            );

        await acao.Should()
            .ThrowAsync<ConflitoDeRequisicaoException>();
    }

    [Fact]
    public async Task DeveRelancarConflitoQuandoRegistroNaoForEncontrado()
    {
        var cenario = new Cenario();
        var excecao = new ConflitoDePersistenciaException("Conflito.");

        cenario.LancamentoRepositorio
            .Setup(x => x.ObterPorCorrelationIdAsync(
                cenario.Requisicao.CorrelationId,
                It.IsAny<CancellationToken>()
                ))
            .ReturnsAsync((Lancamento?)null);

        cenario.UnidadeDeTrabalho
            .Setup(x => x.ExecutarEmTransacaoAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()
                ))
            .ThrowsAsync(excecao);

        var acao = () => cenario.Servico.CriarAsync(
            cenario.Requisicao,
            CancellationToken.None
            );

        var resultado = await acao.Should()
            .ThrowAsync<ConflitoDePersistenciaException>();

        resultado.Which.Should().BeSameAs(excecao);
    }

    [Fact]
    public async Task DeveCriarEventoComTipoDParaDebito()
    {
        await ValidarTipoDoEventoAsync(
            TipoDeLancamento.Debito,
            "D"
            );
    }

    [Fact]
    public async Task DeveCriarEventoComTipoCParaCredito()
    {
        await ValidarTipoDoEventoAsync(
            TipoDeLancamento.Credito,
            "C"
            );
    }

    [Fact]
    public async Task DeveSerializarOsDadosCorretamenteNaOutbox()
    {
        var cenario = new Cenario();

        await cenario.Servico.CriarAsync(cenario.Requisicao, CancellationToken.None);

        cenario.OutboxCapturada.Should().NotBeNull();
        var outbox = cenario.OutboxCapturada!;
        var evento = JsonSerializer.Deserialize<EventoDeLancamentoCriado>(outbox.Payload);

        evento.Should().NotBeNull();
        evento!.CorrelationId.Should().Be(cenario.Requisicao.CorrelationId);
        evento.Payload.DataLancamento.Should().Be(cenario.Requisicao.DataLancamento);
        evento.Payload.Valor.Should().Be(cenario.Requisicao.Valor);
        evento.Payload.Descricao.Should().Be(cenario.Requisicao.Descricao);
    }

    [Fact]
    public async Task DeveRegistrarLogAposSucesso()
    {
        var cenario = new Cenario();

        await cenario.Servico.CriarAsync(cenario.Requisicao, CancellationToken.None);

        cenario.RegistroDeEvento.Verify(
            x => x.Informacao(
                It.IsAny<string>(),
                It.IsAny<object?[]>()
                ),
            Times.Once
            );
    }

    [Fact]
    public async Task NaoDeveRegistrarLogQuandoTransacaoFalhar()
    {
        var cenario = new Cenario();

        cenario.UnidadeDeTrabalho
            .Setup(x => x.ExecutarEmTransacaoAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()
                ))
            .ThrowsAsync(new InvalidOperationException("Falha."));

        var acao = () => cenario.Servico.CriarAsync(
            cenario.Requisicao,
            CancellationToken.None
            );

        await acao.Should().ThrowAsync<InvalidOperationException>();

        cenario.RegistroDeEvento.Verify(
            x => x.Informacao(
                It.IsAny<string>(),
                It.IsAny<object?[]>()
                ),
            Times.Never
            );
    }

    [Fact]
    public async Task DeveObterLancamentoPorId()
    {
        var cenario = new Cenario();
        var lancamento = cenario.CriarLancamentoExistente();

        cenario.LancamentoRepositorio
            .Setup(x => x.ObterPorIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lancamento);

        var resposta = await cenario.Servico.ObterPorIdAsync(10, CancellationToken.None);

        resposta.Should().NotBeNull();
        resposta!.CorrelationId.Should().Be(lancamento.CorrelationId);
    }

    [Fact]
    public async Task DeveRetornarNullQuandoLancamentoNaoExistir()
    {
        var cenario = new Cenario();

        cenario.LancamentoRepositorio
            .Setup(x => x.ObterPorIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Lancamento?)null);

        var resposta = await cenario.Servico.ObterPorIdAsync(10, CancellationToken.None);

        resposta.Should().BeNull();
    }

    private static async Task ValidarTipoDoEventoAsync(
        TipoDeLancamento tipo,
        string tipoEsperado
        )
    {
        var cenario = new Cenario(tipo);

        await cenario.Servico.CriarAsync(cenario.Requisicao, CancellationToken.None);

        var evento = cenario.ObterEventoCapturado();

        evento.Payload.Tipo.Should().Be(tipoEsperado);
    }

    private sealed class Cenario
    {
        public Mock<ILancamentoRepositorio> LancamentoRepositorio { get; } = new();
        public Mock<ILancamentoOutboxRepositorio> OutboxRepositorio { get; } = new();
        public Mock<IUnidadeDeTrabalho> UnidadeDeTrabalho { get; } = new();
        public Mock<IRegistroDeEvento> RegistroDeEvento { get; } = new();
        public CriarLancamentoRequisicao Requisicao { get; }
        public LancamentoServico Servico { get; }
        public LancamentoOutbox? OutboxCapturada { get; private set; }

        public Cenario(TipoDeLancamento tipo = TipoDeLancamento.Credito)
        {
            Requisicao = new CriarLancamentoRequisicao(
                Guid.NewGuid(),
                tipo,
                DateTimeOffset.UtcNow,
                100,
                "Teste"
                );

            LancamentoRepositorio
                .Setup(x => x.ObterPorCorrelationIdAsync(
                    Requisicao.CorrelationId,
                    It.IsAny<CancellationToken>()
                    ))
                .ReturnsAsync((Lancamento?)null);

            UnidadeDeTrabalho
                .Setup(x => x.ExecutarEmTransacaoAsync(
                    It.IsAny<Func<CancellationToken, Task>>(),
                    It.IsAny<CancellationToken>()
                    ))
                .Returns((Func<CancellationToken, Task> operacao, CancellationToken token) => operacao(token));

            UnidadeDeTrabalho
                .Setup(x => x.SalvarAlteracoesAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            OutboxRepositorio
                .Setup(x => x.AdicionarAsync(
                    It.IsAny<LancamentoOutbox>(),
                    It.IsAny<CancellationToken>()
                    ))
                .Callback<LancamentoOutbox, CancellationToken>((outbox, _) => OutboxCapturada = outbox)
                .Returns(Task.CompletedTask);

            Servico = new LancamentoServico(
                LancamentoRepositorio.Object,
                OutboxRepositorio.Object,
                UnidadeDeTrabalho.Object,
                new CriarLancamentoValidador(),
                RegistroDeEvento.Object
                );
        }

        public Lancamento CriarLancamentoExistente()
        {
            return new Lancamento(
                Requisicao.CorrelationId,
                Requisicao.Tipo,
                Requisicao.DataLancamento,
                Requisicao.Valor,
                Requisicao.Descricao
                );
        }

        public EventoDeLancamentoCriado ObterEventoCapturado()
        {
            OutboxCapturada.Should().NotBeNull();
            var outbox = OutboxCapturada!;

            return JsonSerializer.Deserialize<EventoDeLancamentoCriado>(outbox.Payload)!;
        }
    }
}
