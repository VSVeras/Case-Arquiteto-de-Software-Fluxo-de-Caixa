using Testcontainers.PostgreSql;
using Xunit;

namespace LivroRazao.TesteDeIntegracao.Persistencia;

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    public PostgreSqlContainer Container { get; } =
        new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("fluxo_de_caixa_teste")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

    public Task InitializeAsync() => Container.StartAsync();

    public Task DisposeAsync() => Container.DisposeAsync().AsTask();
}
