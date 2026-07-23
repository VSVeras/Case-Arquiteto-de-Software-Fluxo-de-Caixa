using ConsolidadoDiario.Aplicacao.Abstracao;
using ConsolidadoDiario.Aplicacao.Dto;

namespace ConsolidadoDiario.Rest;

public static class RecursoDeConsolidadoDiario
{
    public static IEndpointRouteBuilder MapearRecursosDeConsolidadoDiario(this IEndpointRouteBuilder endpoints)
    {
        var grupo = endpoints
            .MapGroup("/api/consolidados-diarios")
            .WithTags("Consolidado diário");

        grupo.MapGet("/{dataReferencia}", ObterPorDataAsync)
            .WithName("ObterConsolidadoDiarioPorData")
            .Produces<ConsolidadoDiarioResposta>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return endpoints;
    }

    private static async Task<IResult> ObterPorDataAsync(
        DateOnly dataReferencia,
        IConsultaConsolidadoDiarioServico servico,
        CancellationToken cancellationToken
        )
    {
        var resposta = await servico.ObterPorDataAsync(dataReferencia, cancellationToken);

        return resposta is null ? Results.NotFound() : Results.Ok(resposta);
    }
}
