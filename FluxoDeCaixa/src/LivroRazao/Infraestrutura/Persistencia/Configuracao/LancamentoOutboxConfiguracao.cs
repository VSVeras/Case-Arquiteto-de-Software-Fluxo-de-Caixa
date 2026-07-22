using LivroRazao.Infraestrutura.Persistencia.Entidade.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LivroRazao.Infraestrutura.Persistencia.Configuracao;

public sealed class LancamentoOutboxConfiguracao : IEntityTypeConfiguration<LancamentoOutbox>
{
    public void Configure(EntityTypeBuilder<LancamentoOutbox> builder)
    {
        builder.ToTable("TB_LANCAMENTO_OUTBOX");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("ID")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.CorrelationId)
            .HasColumnName("CORRELATION_ID")
            .IsRequired();

        builder.Property(x => x.TipoEvento)
            .HasColumnName("TIPO_EVENTO")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Payload)
            .HasColumnName("PAYLOAD")
            .HasColumnType("jsonb")
            .IsRequired();
        
        builder.Property(x => x.DataOcorrencia)
            .HasColumnName("DATA_OCORRENCIA")
            .IsRequired();
        
        builder.Property(x => x.DataCriacao)
            .HasColumnName("DATA_CRIACAO")
            .IsRequired();
        
        builder.Property(x => x.DataPublicacao)
            .HasColumnName("DATA_PUBLICACAO");
        
        builder.Property(x => x.DataUltimaTentativa)
            .HasColumnName("DATA_ULTIMA_TENTATIVA");
        
        builder.Property(x => x.Tentativas)
            .HasColumnName("TENTATIVAS")
            .IsRequired();
        
        builder.Property(x => x.UltimoErro)
            .HasColumnName("ULTIMO_ERRO")
            .HasMaxLength(2000);
        
        builder.Property(x => x.Status)
            .HasColumnName("STATUS")
            .HasConversion<int>()
            .IsRequired();

        builder.HasIndex(x => x.CorrelationId).IsUnique();

        builder.HasIndex(x => new 
        { 
            x.Status, x.DataCriacao, x.DataUltimaTentativa
        });
    }
}
