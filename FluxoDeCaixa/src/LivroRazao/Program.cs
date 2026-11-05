using LivroRazao.Infraestrutura.Excecao;
using LivroRazao.IoC;
using LivroRazao.Rest;
using Seguranca;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AdicionarDependenciasDaAplicacao(builder.Configuration);

builder.Services.AdicionarSeguranca(builder.Configuration);

if (builder.Environment.IsDevelopment())
{
    builder.Services.AdicionarAutenticacaoDesenvolvimento(builder.Configuration);
}

builder.Services.AddExceptionHandler<TratamentoDeExcecoesGlobal>();

builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.MapearRecursoDeAutenticacaoDesenvolvimento();
}

app.UseHttpsRedirection();

app.UseAuthentication(); // Autentica usuário
app.UseAuthorization();  // Verifica permissões

app.MapHealthChecks("/health");
app.MapearRecursosDeLancamento();

await app.RunAsync();

public partial class Program;
