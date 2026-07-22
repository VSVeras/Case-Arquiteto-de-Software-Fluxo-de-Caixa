var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connectionString = builder.Configuration.GetConnectionString("ConsolidadoDiario")
    ?? throw new InvalidOperationException("A connection string 'ConsolidadoDiario' não foi configurada.");

builder.Services
    .AddHealthChecks()
    .AddNpgSql(connectionString, name: "postgresql");

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapHealthChecks("/health");

await app.RunAsync();

public partial class Program;
