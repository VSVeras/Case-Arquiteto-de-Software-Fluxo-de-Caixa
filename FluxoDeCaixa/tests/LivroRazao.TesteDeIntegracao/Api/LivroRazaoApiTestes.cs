using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using LivroRazao.Aplicacao.Dto;
using LivroRazao.Aplicacao.Servico;
using LivroRazao.Dominio.Caixa;
using LivroRazao.Infraestrutura.Excecoes;
using LivroRazao.Infraestrutura.Mensageria;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace LivroRazao.TesteDeIntegracao.Api;

public sealed class LivroRazaoApiTestes
{
    [Fact]
    public async Task PostValidoDeveRetornar201()
    {
        var servico = new Mock<ILancamentoServico>();
        var requisicao = CriarRequisicao();
        var resposta = CriarResposta(requisicao);

        servico
            .Setup(x => x.CriarAsync(
                It.IsAny<CriarLancamentoRequisicao>(),
                It.IsAny<CancellationToken>()
                ))
            .ReturnsAsync(new CriarLancamentoResultado(resposta, FoiCriado: true));

        using var fabrica = new FabricaDaApi(servico.Object);
        using var cliente = fabrica.CreateClient();

        var resultado = await cliente.PostAsJsonAsync("/api/lancamentos", requisicao);

        resultado.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task PostIdempotenteDeveRetornar200()
    {
        var servico = new Mock<ILancamentoServico>();
        var requisicao = CriarRequisicao();

        servico
            .Setup(x => x.CriarAsync(
                It.IsAny<CriarLancamentoRequisicao>(),
                It.IsAny<CancellationToken>()
                ))
            .ReturnsAsync(
                new CriarLancamentoResultado(
                    CriarResposta(requisicao),
                    FoiCriado: false
                    )
                );

        using var fabrica = new FabricaDaApi(servico.Object);
        using var cliente = fabrica.CreateClient();

        var resultado = await cliente.PostAsJsonAsync("/api/lancamentos", requisicao);

        resultado.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PostComMesmoCorrelationIdEDadosDiferentesDeveRetornar409()
    {
        var servico = new Mock<ILancamentoServico>();

        servico
            .Setup(x => x.CriarAsync(
                It.IsAny<CriarLancamentoRequisicao>(),
                It.IsAny<CancellationToken>()
                ))
            .ThrowsAsync(
                new ConflitoDeRequisicaoException(
                    "O CorrelationId informado já foi utilizado para um lançamento com dados diferentes."
                    )
                );

        using var fabrica = new FabricaDaApi(servico.Object);
        using var cliente = fabrica.CreateClient();

        var resultado = await cliente.PostAsJsonAsync(
            "/api/lancamentos",
            CriarRequisicao()
            );

        resultado.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task PostInvalidoDeveRetornar400ComTodosOsErros()
    {
        var servico = new Mock<ILancamentoServico>();
        var erros = new[]
        {
            new ValidationFailure("CorrelationId", "O correlationId deve ser informado."),
            new ValidationFailure("Tipo", "O tipo deve ser 0 para débito ou 1 para crédito."),
            new ValidationFailure("DataLancamento", "A dataLancamento deve ser informada."),
            new ValidationFailure("Valor", "O valor deve ser maior que zero."),
            new ValidationFailure("Descricao", "A descrição deve possuir no máximo 100 caracteres.")
        };

        servico
            .Setup(x => x.CriarAsync(
                It.IsAny<CriarLancamentoRequisicao>(),
                It.IsAny<CancellationToken>()
                ))
            .ThrowsAsync(new ValidationException(erros));

        using var fabrica = new FabricaDaApi(servico.Object);
        using var cliente = fabrica.CreateClient();

        var resultado = await cliente.PostAsJsonAsync(
            "/api/lancamentos",
            CriarRequisicao()
            );

        var corpo = await resultado.Content.ReadAsStringAsync();

        resultado.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        corpo.Should().Contain("correlationId");
        corpo.Should().Contain("tipo");
        corpo.Should().Contain("dataLancamento");
        corpo.Should().Contain("valor");
        corpo.Should().Contain("descricao");
    }

    [Fact]
    public async Task GetExistenteDeveRetornar200()
    {
        var servico = new Mock<ILancamentoServico>();
        var resposta = CriarResposta(CriarRequisicao());

        servico
            .Setup(x => x.ObterPorIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(resposta);

        using var fabrica = new FabricaDaApi(servico.Object);
        using var cliente = fabrica.CreateClient();

        var resultado = await cliente.GetAsync("/api/lancamentos/1");

        resultado.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetInexistenteDeveRetornar404()
    {
        var servico = new Mock<ILancamentoServico>();

        servico
            .Setup(x => x.ObterPorIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LancamentoResposta?)null);

        using var fabrica = new FabricaDaApi(servico.Object);
        using var cliente = fabrica.CreateClient();

        var resultado = await cliente.GetAsync("/api/lancamentos/1");

        resultado.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static CriarLancamentoRequisicao CriarRequisicao()
    {
        return new CriarLancamentoRequisicao(
            Guid.NewGuid(),
            TipoDeLancamento.Credito,
            DateTimeOffset.UtcNow,
            100,
            "Teste"
            );
    }

    private static LancamentoResposta CriarResposta(CriarLancamentoRequisicao requisicao)
    {
        return new LancamentoResposta(
            1,
            requisicao.CorrelationId,
            requisicao.Tipo,
            requisicao.DataLancamento,
            requisicao.Valor,
            requisicao.Descricao,
            DateTimeOffset.UtcNow
            );
    }

    private sealed class FabricaDaApi(ILancamentoServico servico)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration(
                (_, configuracao) =>
                {
                    configuracao.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["ConnectionStrings:LivroRazao"] =
                                "Host=localhost;Port=5432;Database=teste;Username=postgres;Password=postgres",
                            ["RabbitMq:Host"] = "localhost",
                            ["RabbitMq:Porta"] = "5672",
                            ["RabbitMq:Exchange"] = "teste",
                            ["RabbitMq:RoutingKeyLancamentoCriado"] = "teste",
                            ["RabbitMq:FilaLancamentoCriado"] = "teste",
                            ["PublicadorOutbox:IntervaloEmSegundos"] = "1",
                            ["PublicadorOutbox:LimiteDeTentativas"] = "1",
                            ["PublicadorOutbox:TempoLimiteDeProcessamentoEmMinutos"] = "1"
                        }
                        );
                }
                );

            builder.ConfigureTestServices(
                services =>
                {
                    services.RemoveAll<ILancamentoServico>();
                    services.AddSingleton(servico);

                    services.RemoveAll<IInicializadorRabbitMq>();
                    services.AddSingleton(Mock.Of<IInicializadorRabbitMq>());

                    var servicosHospedados = services
                        .Where(x => x.ServiceType == typeof(IHostedService))
                        .ToArray();

                    foreach (var servicoHospedado in servicosHospedados)
                    {
                        services.Remove(servicoHospedado);
                    }
                }
                );
        }
    }
}
