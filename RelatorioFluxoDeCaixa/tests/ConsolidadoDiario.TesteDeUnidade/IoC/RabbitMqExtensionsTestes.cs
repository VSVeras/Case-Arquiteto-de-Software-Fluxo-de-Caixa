using ConsolidadoDiario.Infraestrutura.Mensageria;
using ConsolidadoDiario.IoC;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace ConsolidadoDiario.TesteDeUnidade.IoC;

public sealed class RabbitMqExtensionsTestes
{
    [Fact]
    public void DeveInicializarRabbitMq()
    {
        var inicializador = new Mock<IInicializadorRabbitMq>();
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton(inicializador.Object);
        var app = builder.Build();

        var resultado = app.InicializarRabbitMq();

        resultado.Should().BeSameAs(app);
        inicializador.Verify(x => x.Inicializar(), Times.Once);
    }
}
