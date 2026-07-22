using System.Text.Json;
using FluentValidation;
using LivroRazao.Aplicacao.Abstracao;
using LivroRazao.Aplicacao.Dto;
using LivroRazao.Aplicacao.EventosDeIntegracao;
using LivroRazao.Dominio.Caixa;
using LivroRazao.Infraestrutura.Excecao;
using LivroRazao.Infraestrutura.Persistencia.Entidade.Outbox;

namespace LivroRazao.Aplicacao.Servico;

public sealed class LancamentoServico(
    ILancamentoRepositorio lancamentoRepositorio,
    ILancamentoOutboxRepositorio outboxRepositorio,
    IUnidadeDeTrabalho unidadeDeTrabalho,
    IValidator<CriarLancamentoRequisicao> validador,
    IRegistroDeEvento registroDeEvento
    ) : ILancamentoServico
{
    public async Task<CriarLancamentoResultado> CriarAsync(
        CriarLancamentoRequisicao requisicao,
        CancellationToken cancellationToken
        )
    {
        await validador.ValidateAndThrowAsync(requisicao, cancellationToken);

        var lancamentoExistente =
            await lancamentoRepositorio.ObterPorCorrelationIdAsync(
                requisicao.CorrelationId,
                cancellationToken
                );

        if (lancamentoExistente is not null)
        {
            return new CriarLancamentoResultado(
                ValidarLancamentoExistente(lancamentoExistente, requisicao),
                FoiCriado: false
                );
        }

        var lancamento = new Lancamento(
            requisicao.CorrelationId,
            requisicao.Tipo,
            requisicao.DataLancamento,
            requisicao.Valor,
            requisicao.Descricao
            );

        try
        {
            await unidadeDeTrabalho.ExecutarEmTransacaoAsync(
                async token =>
                {
                    await lancamentoRepositorio.AdicionarAsync(lancamento, token);

                    await unidadeDeTrabalho.SalvarAlteracoesAsync(token);

                    var eventoDeLancamentoCriado =
                        new EventoDeLancamentoCriado(
                            CorrelationId: lancamento.CorrelationId,
                            TipoEvento: nameof(EventoDeLancamentoCriado),
                            DataOcorrencia: DateTimeOffset.UtcNow,
                            Payload: new DadosDoLancamento(
                                LancamentoId: lancamento.Id,
                                Tipo: ConverterTipoParaEvento(lancamento.Tipo),
                                DataLancamento: lancamento.DataLancamento,
                                Valor: lancamento.Valor,
                                Descricao: lancamento.Descricao
                                )
                            );

                    var eventoOutbox =
                        new LancamentoOutbox(
                            correlationId: eventoDeLancamentoCriado.CorrelationId,
                            tipoEvento: eventoDeLancamentoCriado.TipoEvento,
                            payload: JsonSerializer.Serialize(eventoDeLancamentoCriado),
                            dataOcorrencia: eventoDeLancamentoCriado.DataOcorrencia
                            );

                    await outboxRepositorio.AdicionarAsync(
                        eventoOutbox,
                        token
                        );

                    await unidadeDeTrabalho.SalvarAlteracoesAsync(token);
                },
                cancellationToken
                );
        }
        catch (ConflitoDePersistenciaException)
        {
            unidadeDeTrabalho.LimparRastreamento();

            var lancamentoCriadoConcorrentemente =
                await lancamentoRepositorio.ObterPorCorrelationIdAsync(
                    requisicao.CorrelationId,
                    cancellationToken
                    );

            if (lancamentoCriadoConcorrentemente is null)
            {
                throw;
            }

            return new CriarLancamentoResultado(
                ValidarLancamentoExistente(lancamentoCriadoConcorrentemente, requisicao),
                FoiCriado: false
                );
        }

        registroDeEvento.Informacao(
            "Lançamento {LancamentoId} criado. CorrelationId {CorrelationId}.",
            lancamento.Id,
            lancamento.CorrelationId
            );

        return new CriarLancamentoResultado(MapearResposta(lancamento), FoiCriado: true);
    }

    public async Task<LancamentoResposta?> ObterPorIdAsync(long id, CancellationToken cancellationToken)
    {
        var lancamento = await lancamentoRepositorio.ObterPorIdAsync(id, cancellationToken);

        return lancamento is null ? null : MapearResposta(lancamento);
    }

    private static LancamentoResposta ValidarLancamentoExistente(
        Lancamento lancamentoExistente,
        CriarLancamentoRequisicao requisicao
        )
    {
        if (!EhAMesmaOperacao(lancamentoExistente, requisicao))
        {
            throw new ConflitoDeRequisicaoException(
                "O CorrelationId informado já foi utilizado para um lançamento com dados diferentes."
                );
        }

        return MapearResposta(lancamentoExistente);
    }

    private static bool EhAMesmaOperacao(Lancamento lancamentoExistente, CriarLancamentoRequisicao requisicao)
    {
        return lancamentoExistente.CorrelationId == requisicao.CorrelationId
            && lancamentoExistente.RepresentaMesmaOperacao(
                requisicao.Tipo,
                requisicao.DataLancamento,
                requisicao.Valor,
                requisicao.Descricao
                );
    }

    private static string ConverterTipoParaEvento(TipoDeLancamento tipo)
    {
        return tipo switch
        {
            TipoDeLancamento.Debito => "D",
            TipoDeLancamento.Credito => "C",
            _ => throw new InvalidOperationException("Tipo de lançamento inválido.")
        };
    }

    private static LancamentoResposta MapearResposta(Lancamento lancamento)
    {
        return new LancamentoResposta(
            lancamento.Id,
            lancamento.CorrelationId,
            lancamento.Tipo,
            lancamento.DataLancamento,
            lancamento.Valor,
            lancamento.Descricao,
            lancamento.DataCriacao
            );
    }
}
