using LivroRazao.Dominio.Caixa;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LivroRazao.Infraestrutura.Persistencia.Configuracao;

public sealed class LancamentoConfiguracao :
    IEntityTypeConfiguration<Lancamento>
{
    public void Configure(
        EntityTypeBuilder<Lancamento> builder)
    {
        builder.ToTable("TB_LANCAMENTO");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("ID")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.CorrelationId)
            .HasColumnName("CORRELATION_ID")
            .IsRequired();

        builder.Property(x => x.Tipo)
            .HasColumnName("TIPO")
            .HasColumnType("char(1)")
            .IsFixedLength()
            .HasMaxLength(1)
            .HasConversion(
                tipo => ConverterParaBanco(tipo),
                valor => ConverterParaDominio(valor))
            .IsRequired();

        builder.Property(x => x.DataLancamento)
            .HasColumnName("DATA_LANCAMENTO")
            .IsRequired();

        builder.Property(x => x.Valor)
            .HasColumnName("VALOR")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.Descricao)
            .HasColumnName("DESCRICAO")
            .HasMaxLength(100);

        builder.Property(x => x.DataCriacao)
            .HasColumnName("DATA_CRIACAO")
            .IsRequired();

        builder.HasIndex(x => x.CorrelationId)
            .IsUnique();
    }

    private static string ConverterParaBanco(TipoDeLancamento tipo)
    {
        return tipo switch
        {
            TipoDeLancamento.Debito => "D",
            TipoDeLancamento.Credito => "C",
            _ => throw new InvalidOperationException($"Tipo de lançamento inválido: {tipo}.")
        };
    }

    private static TipoDeLancamento ConverterParaDominio(string valor)
    {
        return valor switch
        {
            "D" => TipoDeLancamento.Debito,
            "C" => TipoDeLancamento.Credito,
            _ => throw new InvalidOperationException($"Código de tipo de lançamento inválido: {valor}.")
        };
    }
}

