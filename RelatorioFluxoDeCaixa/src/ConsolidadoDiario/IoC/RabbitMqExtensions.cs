using ConsolidadoDiario.Infraestrutura.Mensageria;

namespace ConsolidadoDiario.IoC;

public static class RabbitMqExtensions
{
    public static WebApplication InicializarRabbitMq(this WebApplication app)
    {
        using var escopo = app.Services.CreateScope();

        var inicializador = escopo.ServiceProvider.GetRequiredService<IInicializadorRabbitMq>();
        inicializador.Inicializar();

        return app;
    }
}
