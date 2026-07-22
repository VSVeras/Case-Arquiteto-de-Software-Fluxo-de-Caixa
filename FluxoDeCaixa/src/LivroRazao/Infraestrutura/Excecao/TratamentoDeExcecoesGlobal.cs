using System.Text.Json;
using FluentValidation;
using LivroRazao.Aplicacao.Abstracao;
using LivroRazao.Dominio;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace LivroRazao.Infraestrutura.Excecao;
public sealed class TratamentoDeExcecoesGlobal(
    IRegistroDeEvento registroDeEvento)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception excecao,
        CancellationToken cancellationToken)
    {
        return excecao switch
        {
            ValidationException validacao =>
                await TratarErroDeValidacaoAsync(
                    httpContext,
                    validacao,
                    cancellationToken),

            BadHttpRequestException requisicaoInvalida =>
                await TratarRequisicaoInvalidaAsync(
                    httpContext,
                    requisicaoInvalida,
                    cancellationToken),

            JsonException jsonInvalido =>
                await TratarJsonInvalidoAsync(
                    httpContext,
                    jsonInvalido,
                    cancellationToken),

            ExcecaoDeDominio dominio =>
                await TratarErroDeDominioAsync(
                    httpContext,
                    dominio,
                    cancellationToken),

            ConflitoDeRequisicaoException conflito =>
                await TratarConflitoAsync(
                    httpContext,
                    conflito,
                    cancellationToken),

            _ =>
                await TratarErroInternoAsync(
                    httpContext,
                    excecao,
                    cancellationToken)
        };
    }

    private async ValueTask<bool> TratarErroDeValidacaoAsync(
        HttpContext httpContext,
        ValidationException excecao,
        CancellationToken cancellationToken)
    {
        registroDeEvento.Aviso(
            excecao,
            "Requisição rejeitada por falha de validação. " +
            "TraceIdentifier {TraceIdentifier}.",
            httpContext.TraceIdentifier);

        var erros = excecao.Errors
            .GroupBy(erro => ConverterNomeDaPropriedade(erro.PropertyName))
            .ToDictionary(
                grupo => grupo.Key,
                grupo => grupo
                    .Select(erro => erro.ErrorMessage)
                    .Distinct()
                    .ToArray());

        if (erros.Count == 0)
        {
            erros.Add(
                "requisicao",
                [excecao.Message]);
        }

        await EscreverProblemaDeValidacaoAsync(
            httpContext,
            erros,
            cancellationToken);

        return true;
    }

    private async ValueTask<bool> TratarRequisicaoInvalidaAsync(
        HttpContext httpContext,
        BadHttpRequestException excecao,
        CancellationToken cancellationToken)
    {
        registroDeEvento.Aviso(
            excecao,
            "Requisição HTTP inválida. " +
            "TraceIdentifier {TraceIdentifier}.",
            httpContext.TraceIdentifier);

        var mensagem = excecao.InnerException is JsonException
            ? "O corpo da requisição contém JSON inválido ou campos incompatíveis com o contrato."
            : "A requisição enviada é inválida.";

        await EscreverProblemaDeValidacaoAsync(
            httpContext,
            new Dictionary<string, string[]>
            {
                ["requisicao"] = [mensagem]
            },
            cancellationToken);

        return true;
    }

    private async ValueTask<bool> TratarJsonInvalidoAsync(
        HttpContext httpContext,
        JsonException excecao,
        CancellationToken cancellationToken)
    {
        registroDeEvento.Aviso(
            excecao,
            "JSON inválido recebido. " +
            "TraceIdentifier {TraceIdentifier}.",
            httpContext.TraceIdentifier);

        await EscreverProblemaDeValidacaoAsync(
            httpContext,
            new Dictionary<string, string[]>
            {
                ["requisicao"] =
                [
                    "O corpo da requisição contém JSON inválido ou campos incompatíveis com o contrato."
                ]
            },
            cancellationToken);

        return true;
    }

    private async ValueTask<bool> TratarConflitoAsync(
        HttpContext httpContext,
        ConflitoDeRequisicaoException excecao,
        CancellationToken cancellationToken)
    {
        httpContext.Response.StatusCode =
            StatusCodes.Status409Conflict;

        await httpContext.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflito na solicitação.",
                Detail = excecao.Message,
                Instance = httpContext.Request.Path,
                Extensions =
                {
                    ["traceId"] = httpContext.TraceIdentifier
                }
            },
            cancellationToken);

        return true;
    }

    private async ValueTask<bool> TratarErroInternoAsync(
        HttpContext httpContext,
        Exception excecao,
        CancellationToken cancellationToken)
    {
        registroDeEvento.Erro(
            excecao,
            "Erro não tratado durante a requisição HTTP. " +
            "TraceIdentifier {TraceIdentifier}.",
            httpContext.TraceIdentifier);

        httpContext.Response.StatusCode =
            StatusCodes.Status500InternalServerError;

        await httpContext.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Erro interno.",
                Detail = "Ocorreu um erro ao processar a solicitação.",
                Instance = httpContext.Request.Path,
                Extensions =
                {
                    ["traceId"] = httpContext.TraceIdentifier
                }
            },
            cancellationToken);

        return true;
    }

    private static async Task EscreverProblemaDeValidacaoAsync(
        HttpContext httpContext,
        IDictionary<string, string[]> erros,
        CancellationToken cancellationToken)
    {
        httpContext.Response.StatusCode =
            StatusCodes.Status400BadRequest;

        var resposta = new ValidationProblemDetails(erros)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "A requisição possui campos inválidos.",
            Detail = "Corrija os campos informados e envie a solicitação novamente.",
            Instance = httpContext.Request.Path
        };

        resposta.Extensions["traceId"] =
            httpContext.TraceIdentifier;

        await httpContext.Response.WriteAsJsonAsync(
            resposta,
            cancellationToken);
    }

    private async ValueTask<bool> TratarErroDeDominioAsync(
        HttpContext httpContext,
        ExcecaoDeDominio excecao,
        CancellationToken cancellationToken)
    {
        registroDeEvento.Aviso(
            excecao,
            "Requisição rejeitada por violação de invariantes do domínio. " +
            "TraceIdentifier {TraceIdentifier}.",
            httpContext.TraceIdentifier);

        await EscreverProblemaDeValidacaoAsync(
            httpContext, 
            new Dictionary<string, string[]>
            {
                ["requisicao"] = excecao.Erros.ToArray()
            },
            cancellationToken);

        return true;
    }

    private static string ConverterNomeDaPropriedade(
        string nomeDaPropriedade)
    {
        if (string.IsNullOrWhiteSpace(nomeDaPropriedade))
        {
            return "requisicao";
        }

        return char.ToLowerInvariant(nomeDaPropriedade[0]) + nomeDaPropriedade[1..];
    }
}
