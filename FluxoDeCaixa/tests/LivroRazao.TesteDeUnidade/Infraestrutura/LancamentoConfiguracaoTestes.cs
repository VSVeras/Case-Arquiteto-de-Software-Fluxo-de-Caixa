using FluentAssertions;
using LivroRazao.Dominio.Caixa;
using LivroRazao.Infraestrutura.Persistencia.Configuracao;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Xunit;

namespace LivroRazao.TesteDeUnidade.Infraestrutura;

public sealed class LancamentoConfiguracaoTestes
{
    [Fact]
    public void DeveConfigurarMapeamentoEsperado()
    {
        var modelBuilder = CriarModelBuilder();

        var entidade = modelBuilder.Model.FindEntityType(typeof(Lancamento));

        entidade.Should().NotBeNull();

        var entidadeConfigurada = entidade!;

        entidadeConfigurada
            .GetTableName()
            .Should()
            .Be("TB_LANCAMENTO");

        var chavePrimaria = entidadeConfigurada.FindPrimaryKey();

        chavePrimaria.Should().NotBeNull();

        chavePrimaria!
            .Properties
            .Should()
            .ContainSingle(x => x.Name == nameof(Lancamento.Id));

        var propriedadeCorrelationId =
            entidadeConfigurada.FindProperty(nameof(Lancamento.CorrelationId));

        propriedadeCorrelationId.Should().NotBeNull();

        var indiceCorrelationId =
            entidadeConfigurada.FindIndex(propriedadeCorrelationId!);

        indiceCorrelationId.Should().NotBeNull();

        indiceCorrelationId!
            .IsUnique
            .Should()
            .BeTrue();

        var propriedadeDescricao =
            entidadeConfigurada.FindProperty(nameof(Lancamento.Descricao));

        propriedadeDescricao.Should().NotBeNull();

        propriedadeDescricao!
            .GetMaxLength()
            .Should()
            .Be(100);

        var propriedadeValor =
            entidadeConfigurada.FindProperty(nameof(Lancamento.Valor));

        propriedadeValor.Should().NotBeNull();

        propriedadeValor!
            .GetPrecision()
            .Should()
            .Be(18);

        propriedadeValor
            .GetScale()
            .Should()
            .Be(2);
    }

    [Theory]
    [InlineData(TipoDeLancamento.Debito, "D")]
    [InlineData(TipoDeLancamento.Credito, "C")]
    public void ConversorDeveConverterTipoParaBanco(
        TipoDeLancamento tipo,
        string esperado)
    {
        var conversor = ObterConversor();

        var resultado = conversor.ConvertToProvider(tipo);

        resultado.Should().Be(esperado);
    }

    [Theory]
    [InlineData("D", TipoDeLancamento.Debito)]
    [InlineData("C", TipoDeLancamento.Credito)]
    public void ConversorDeveConverterBancoParaTipo(
        string valor,
        TipoDeLancamento esperado)
    {
        var conversor = ObterConversor();

        var resultado = conversor.ConvertFromProvider(valor);

        resultado.Should().Be(esperado);
    }

    [Fact]
    public void ConversorDeveRejeitarTipoInvalido()
    {
        var conversor = ObterConversor();

        var acao = () => conversor.ConvertToProvider((TipoDeLancamento)999);

        acao.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*Tipo de lançamento inválido*");
    }

    [Fact]
    public void ConversorDeveRejeitarCodigoInvalido()
    {
        var conversor = ObterConversor();

        var acao = () => conversor.ConvertFromProvider("X");

        acao.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*Código de tipo de lançamento inválido*");
    }

    private static Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter ObterConversor()
    {
        var modelBuilder = CriarModelBuilder();

        return modelBuilder.Model
            .FindEntityType(typeof(Lancamento))!
            .FindProperty(nameof(Lancamento.Tipo))!
            .GetValueConverter()!;
    }

    private static ModelBuilder CriarModelBuilder()
    {
        var modelBuilder = new ModelBuilder(new ConventionSet());
        var configuracao = new LancamentoConfiguracao();

        configuracao.Configure(modelBuilder.Entity<Lancamento>());

        return modelBuilder;
    }
}
