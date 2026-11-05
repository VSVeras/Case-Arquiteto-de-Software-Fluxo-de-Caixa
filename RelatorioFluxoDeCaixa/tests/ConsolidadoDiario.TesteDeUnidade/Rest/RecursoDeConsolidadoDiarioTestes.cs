using ConsolidadoDiario.Aplicacao.Abstracao;
using ConsolidadoDiario.Rest;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace ConsolidadoDiario.TesteDeUnidade.Rest;

public sealed class RecursoDeConsolidadoDiarioTestes
{
    [Fact]
    public void DeveMapearRecursoDeConsolidadoDiario()
    {
        var builder = WebApplication.CreateBuilder();

        var servico = new Mock<IConsultaConsolidadoDiarioServico>();

        builder.Services.AddScoped(_ => servico.Object);

        var app = builder.Build();

        var resultado = app.MapearRecursosDeConsolidadoDiario();

        resultado.Should().BeSameAs(app);

        var endpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(x => x.Endpoints)
            .OfType<RouteEndpoint>()
            .ToArray();

        endpoints.Should().ContainSingle(x =>
            x.RoutePattern.RawText == "/api/consolidados-diarios/{dataReferencia}");
    }
}
