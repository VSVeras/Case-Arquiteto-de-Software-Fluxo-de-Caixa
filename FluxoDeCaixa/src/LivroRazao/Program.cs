using LivroRazao.Infraestrutura.Excecao;
using LivroRazao.IoC;
using LivroRazao.Rest;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AdicionarDependenciasDaAplicacao(builder.Configuration);

builder.Services.AddExceptionHandler<TratamentoDeExcecoesGlobal>();

builder.Services.AddProblemDetails();

var app = builder.Build();

app.InicializarRabbitMq();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapHealthChecks("/health");
app.MapearRecursosDeLancamento();

await app.RunAsync();

public partial class Program;
