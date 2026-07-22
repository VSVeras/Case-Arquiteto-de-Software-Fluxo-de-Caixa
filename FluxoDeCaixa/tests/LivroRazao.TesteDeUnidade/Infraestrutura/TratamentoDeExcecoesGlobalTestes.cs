using System.Text.Json;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using LivroRazao.Aplicacao.Abstracao;
using LivroRazao.Dominio;
using LivroRazao.Infraestrutura.Excecoes;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace LivroRazao.TesteDeUnidade.Infraestrutura;

public sealed class TratamentoDeExcecoesGlobalTestes
{
    [Fact]
    public async Task DeveRetornar400ParaValidationException()
    {
        var excecao = new ValidationException(
            [new ValidationFailure("CorrelationId", "Inválido.")]
            );

        var contexto = await ExecutarAsync(excecao);

        contexto.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task DeveRetornar400ParaExcecaoDeDominio()
    {
        var contexto = await ExecutarAsync(
            new ExcecaoDeDominio(["Erro de domínio."])
            );

        contexto.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        (await LerCorpoAsync(contexto)).Should().Contain("Erro de domínio.");
    }

    [Fact]
    public async Task DeveRetornar400ParaJsonInvalido()
    {
        var contexto = await ExecutarAsync(new JsonException("JSON inválido."));

        contexto.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task DeveRetornar400ParaBadHttpRequestException()
    {
        var contexto = await ExecutarAsync(new BadHttpRequestException("Inválida."));

        contexto.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task DeveRetornar409ParaConflitoDeRequisicaoException()
    {
        var contexto = await ExecutarAsync(
            new ConflitoDeRequisicaoException("Conflito.")
            );

        contexto.Response.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task DeveRetornar500ParaErroInesperado()
    {
        var contexto = await ExecutarAsync(new InvalidOperationException("Falha."));

        contexto.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public async Task DeveAdicionarTraceId()
    {
        var contexto = await ExecutarAsync(
            new ConflitoDeRequisicaoException("Conflito."),
            "trace-teste"
            );

        (await LerCorpoAsync(contexto)).Should().Contain("trace-teste");
    }

    [Fact]
    public async Task DeveConverterNomeDePropriedadeParaCamelCase()
    {
        var excecao = new ValidationException(
            [new ValidationFailure("CorrelationId", "Inválido.")]
            );

        var contexto = await ExecutarAsync(excecao);

        (await LerCorpoAsync(contexto)).Should().Contain("correlationId");
    }

    private static async Task<DefaultHttpContext> ExecutarAsync(
        Exception excecao,
        string traceIdentifier = "trace-id"
        )
    {
        var contexto = new DefaultHttpContext
        {
            TraceIdentifier = traceIdentifier
        };

        contexto.Request.Path = "/api/lancamentos";
        contexto.Response.Body = new MemoryStream();

        var tratamento = new TratamentoDeExcecoesGlobal(
            Mock.Of<IRegistroDeEvento>()
            );

        var tratada = await tratamento.TryHandleAsync(
            contexto,
            excecao,
            CancellationToken.None
            );

        tratada.Should().BeTrue();

        return contexto;
    }

    private static async Task<string> LerCorpoAsync(DefaultHttpContext contexto)
    {
        contexto.Response.Body.Position = 0;

        using var leitor = new StreamReader(contexto.Response.Body, leaveOpen: true);

        return await leitor.ReadToEndAsync();
    }
}
