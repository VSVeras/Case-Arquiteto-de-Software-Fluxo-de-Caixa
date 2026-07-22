using LivroRazao.Infraestrutura.Mensageria;

namespace LivroRazao.IoC;

public static class RabbitMqExtensions
{
    public static WebApplication InicializarRabbitMq(this WebApplication app)
    {
        using var escopo = app.Services.CreateScope();

        // Inicializa a topologia do RabbitMQ uma única vez na inicialização da aplicação.
        var inicializador =
            escopo.ServiceProvider
                .GetRequiredService<IInicializadorRabbitMq>();

        inicializador.Inicializar();

        return app;
    }
}
