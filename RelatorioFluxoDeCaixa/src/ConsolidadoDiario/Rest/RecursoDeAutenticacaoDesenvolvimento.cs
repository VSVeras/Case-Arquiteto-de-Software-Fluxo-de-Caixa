using Seguranca.Autenticacao;

namespace ConsolidadoDiario.Rest;

public static class RecursoDeAutenticacaoDesenvolvimento
{
    public static IEndpointRouteBuilder MapearRecursoDeAutenticacaoDesenvolvimento(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
                "/api/dev/autenticacao/token",
                (AutenticacaoRequisicao requisicao, IAutenticacaoDesenvolvimento autenticacao) =>
                {
                    var resposta = autenticacao.Autenticar(requisicao);
                    return resposta is null ? Results.Unauthorized() : Results.Ok(resposta);
                })
            .AllowAnonymous()
            .WithTags("Autenticação DEV")
            .WithName("AutenticarDesenvolvimento")
            .Produces<AutenticacaoResposta>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        return endpoints;
    }
}
