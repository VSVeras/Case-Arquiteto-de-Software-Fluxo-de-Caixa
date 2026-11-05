using System.Text.Json;
using ConsolidadoDiario.Aplicacao.Abstracao;
using ConsolidadoDiario.Infraestrutura.Excecao;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace ConsolidadoDiario.TesteDeUnidade.Infraestrutura;

public sealed class TratamentoDeExcecoesGlobalTestes
{
    [Fact]
    public async Task DeveAdicionarTraceId()
    {
        var contexto = await ExecutarAsync(
            new InvalidOperationException("Falha."),
            "trace-teste");

        (await LerCorpoAsync(contexto)).Should().Contain("trace-teste");
    }

    [Fact]
    public async Task DeveConverterNomeDePropriedadeParaCamelCase()
    {
        var excecao = new ValidationException(
            [new ValidationFailure("DataReferencia", "Inválida.")]);

        var contexto = await ExecutarAsync(excecao);

        (await LerCorpoAsync(contexto)).Should().Contain("dataReferencia");
    }

    [Fact]
    public async Task DeveRetornar400ParaBadHttpRequestException()
    {
        var contexto = await ExecutarAsync(
            new BadHttpRequestException("Inválida."));

        contexto.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task DeveRetornar400ParaJsonInvalido()
    {
        var contexto = await ExecutarAsync(
            new JsonException("JSON inválido."));

        contexto.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task DeveRetornar400ParaValidationException()
    {
        var excecao = new ValidationException(
            [new ValidationFailure("DataReferencia", "Inválida.")]);

        var contexto = await ExecutarAsync(excecao);

        contexto.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task DeveRetornar500ParaErroInesperado()
    {
        var contexto = await ExecutarAsync(
            new InvalidOperationException("Falha."));

        contexto.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        (await LerCorpoAsync(contexto)).Should().Contain("Erro interno.");
    }

    private static async Task<DefaultHttpContext> ExecutarAsync(
        Exception excecao,
        string traceIdentifier = "trace-id")
    {
        var contexto = new DefaultHttpContext
        {
            TraceIdentifier = traceIdentifier
        };

        contexto.Request.Path = "/api/consolidados-diarios/2026-08-25";
        contexto.Response.Body = new MemoryStream();

        var tratamento = new TratamentoDeExcecoesGlobal(
            Mock.Of<IRegistroDeEvento>());

        var tratada = await tratamento.TryHandleAsync(
            contexto,
            excecao,
            CancellationToken.None);

        tratada.Should().BeTrue();

        return contexto;
    }

    private static async Task<string> LerCorpoAsync(
        DefaultHttpContext contexto)
    {
        contexto.Response.Body.Position = 0;

        using var leitor = new StreamReader(
            contexto.Response.Body,
            leaveOpen: true);

        return await leitor.ReadToEndAsync();
    }
}
