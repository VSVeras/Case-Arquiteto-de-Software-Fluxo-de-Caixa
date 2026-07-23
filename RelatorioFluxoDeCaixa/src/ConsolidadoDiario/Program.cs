using ConsolidadoDiario.IoC;
using ConsolidadoDiario.Rest;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AdicionarDependenciasDaAplicacao(builder.Configuration);

var app = builder.Build();

app.InicializarRabbitMq();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapHealthChecks("/health");
app.MapearRecursosDeConsolidadoDiario();

await app.RunAsync();

public partial class Program;
