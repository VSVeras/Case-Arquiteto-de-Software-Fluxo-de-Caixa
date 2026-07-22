using LivroRazao.Aplicacao.Dto;
using LivroRazao.Aplicacao.Servico;

namespace LivroRazao.Rest;

public static class RecursoDeLancamento
{
    public static IEndpointRouteBuilder MapearRecursosDeLancamento(
        this IEndpointRouteBuilder endpoints)
    {
        var grupo = endpoints
            .MapGroup("/api/lancamentos")
            .WithTags("Lancamentos");

        grupo.MapPost("/", CriarLancamentoAsync)
            .Produces<LancamentoResposta>(StatusCodes.Status201Created)
            .Produces<LancamentoResposta>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        grupo.MapGet("/{id:long}", ObterLancamentoPorIdAsync);

        return endpoints;
    }

    private static async Task<IResult> CriarLancamentoAsync(
        CriarLancamentoRequisicao requisicao,
        ILancamentoServico servico,
        CancellationToken cancellationToken
        )
    {
        var resultado = await servico.CriarAsync(requisicao, cancellationToken);

        if (!resultado.FoiCriado)
        {
            return Results.Ok(resultado.Resposta);
        }

        return Results.Created(
            $"/api/lancamentos/{resultado.Resposta.Id}",
            resultado.Resposta
            );
    }

    private static async Task<IResult> ObterLancamentoPorIdAsync(
        long id,
        ILancamentoServico servico,
        CancellationToken cancellationToken
        )
    {
        var resposta = await servico.ObterPorIdAsync(id, cancellationToken);

        return resposta is null ? Results.NotFound() : Results.Ok(resposta);
    }
}
