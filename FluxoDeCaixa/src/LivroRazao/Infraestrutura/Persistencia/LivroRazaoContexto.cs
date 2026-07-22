using LivroRazao.Dominio.Caixa;
using LivroRazao.Infraestrutura.Persistencia.Entidades.Outbox;
using Microsoft.EntityFrameworkCore;

namespace LivroRazao.Infraestrutura.Persistencia;

public sealed class LivroRazaoContexto(DbContextOptions<LivroRazaoContexto> options)
    : DbContext(options)
{
    public DbSet<Lancamento> Lancamentos => Set<Lancamento>();
    public DbSet<LancamentoOutbox> LancamentosOutbox => Set<LancamentoOutbox>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LivroRazaoContexto).Assembly);
    }
}
