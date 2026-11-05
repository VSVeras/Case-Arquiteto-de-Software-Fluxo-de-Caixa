using Testcontainers.PostgreSql;
using Xunit;

namespace LivroRazao.TesteDeIntegracao.Persistencia;

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    public PostgreSqlContainer Container { get; } =
        new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("fluxo_de_caixa_teste")
            .WithUsername($"teste_{Guid.NewGuid():N}")
            .WithPassword(Guid.NewGuid().ToString("N"))
            .Build();

    public Task DisposeAsync() => Container.DisposeAsync().AsTask();

    public Task InitializeAsync() => Container.StartAsync();
}
