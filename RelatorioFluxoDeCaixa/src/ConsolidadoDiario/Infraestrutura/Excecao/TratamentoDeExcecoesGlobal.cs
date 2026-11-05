using System.Text.Json;
using ConsolidadoDiario.Aplicacao.Abstracao;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;


namespace ConsolidadoDiario.Infraestrutura.Excecao
{
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

                _ =>
                    await TratarErroInternoAsync(
                        httpContext,
                        excecao,
                        cancellationToken)
            };
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
    }
}
