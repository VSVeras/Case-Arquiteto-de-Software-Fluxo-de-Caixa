using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using LivroRazao.Aplicacao.Dto;
using LivroRazao.Aplicacao.Servico;
using LivroRazao.Dominio.Caixa;
using LivroRazao.Infraestrutura.Excecao;
using Mensageria.RabbitMQ.Topologia;
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
    public async Task GetExistenteDeveRetornar200()
    {
        var servico = new Mock<ILancamentoServico>();
        var resposta = CriarResposta(CriarRequisicao());

        servico
            .Setup(x => x.ObterPorIdAsync(
                1,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(resposta);

        using var fabrica = new FabricaDaApi(servico.Object);
        using var cliente = CriarClienteAutenticado(fabrica);

        var resultado =
            await cliente.GetAsync("/api/lancamentos/1");

        resultado.StatusCode.Should()
            .Be(HttpStatusCode.OK);

        servico.Verify(
            x => x.ObterPorIdAsync(
                1,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetInexistenteDeveRetornar404()
    {
        var servico = new Mock<ILancamentoServico>();

        servico
            .Setup(x => x.ObterPorIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LancamentoResposta?)null);

        using var fabrica = new FabricaDaApi(servico.Object);
        using var cliente = CriarClienteAutenticado(fabrica);

        var resultado = await cliente.GetAsync("/api/lancamentos/1");

        resultado.StatusCode.Should().Be(HttpStatusCode.NotFound);

        servico.Verify(
            x => x.ObterPorIdAsync(
                1,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetSemTokenDeveRetornar401()
    {
        var servico = new Mock<ILancamentoServico>();

        using var fabrica = new FabricaDaApi(servico.Object);
        using var cliente = fabrica.CreateClient();

        var resultado = await cliente.GetAsync("/api/lancamentos/1");

        resultado.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        servico.Verify(
            x => x.ObterPorIdAsync(
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
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
        using var cliente = CriarClienteAutenticado(fabrica);

        var resultado = await cliente.PostAsJsonAsync(
            "/api/lancamentos",
            CriarRequisicao()
            );

        resultado.StatusCode.Should().Be(HttpStatusCode.Conflict);

        servico.Verify(
            x => x.CriarAsync(
                It.IsAny<CriarLancamentoRequisicao>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
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
        using var cliente = CriarClienteAutenticado(fabrica);

        var resultado = await cliente.PostAsJsonAsync(
            "/api/lancamentos",
            requisicao);

        resultado.StatusCode.Should().Be(HttpStatusCode.OK);

        servico.Verify(
            x => x.CriarAsync(
                It.IsAny<CriarLancamentoRequisicao>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PostInvalidoDeveRetornar400ComTodosOsErros()
    {
        var servico = new Mock<ILancamentoServico>();

        var erros = new[]
        {
            new ValidationFailure(
                "CorrelationId",
                "O correlationId deve ser informado."),

            new ValidationFailure(
                "Tipo",
                "O tipo deve ser 0 para débito ou 1 para crédito."),

            new ValidationFailure(
                "DataLancamento",
                "A dataLancamento deve ser informada."),

            new ValidationFailure(
                "Valor",
                "O valor deve ser maior que zero."),

            new ValidationFailure(
                "Descricao",
                "A descrição deve possuir no máximo 100 caracteres.")
        };

        servico
            .Setup(x => x.CriarAsync(
                It.IsAny<CriarLancamentoRequisicao>(),
                It.IsAny<CancellationToken>()
                ))
            .ThrowsAsync(new ValidationException(erros));

        using var fabrica = new FabricaDaApi(servico.Object);
        using var cliente = CriarClienteAutenticado(fabrica);

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

        servico.Verify(
            x => x.CriarAsync(
                It.IsAny<CriarLancamentoRequisicao>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PostValidoDeveRetornar201()
    {
        var servico = new Mock<ILancamentoServico>();
        var requisicao = CriarRequisicao();
        var resposta = CriarResposta(requisicao);

        servico
            .Setup(x => x.CriarAsync(
                It.IsAny<CriarLancamentoRequisicao>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new CriarLancamentoResultado(
                    resposta,
                    FoiCriado: true));

        using var fabrica = new FabricaDaApi(servico.Object);
        using var cliente = CriarClienteAutenticado(fabrica);

        var resultado = await cliente.PostAsJsonAsync(
            "/api/lancamentos",
            requisicao);

        resultado.StatusCode.Should()
            .Be(HttpStatusCode.Created);

        servico.Verify(
            x => x.CriarAsync(
                It.Is<CriarLancamentoRequisicao>(
                    r => r.CorrelationId == requisicao.CorrelationId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetComTokenInvalidoDeveRetornar401()
    {
        var servico = new Mock<ILancamentoServico>();

        using var fabrica = new FabricaDaApi(servico.Object);
        using var cliente = fabrica.CreateClient();

        cliente.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                "token-jwt-invalido");

        var resultado = await cliente.GetAsync("/api/lancamentos/1");

        resultado.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetComAssinaturaJwtInvalidaDeveRetornar401()
    {
        var servico = new Mock<ILancamentoServico>();

        using var fabrica = new FabricaDaApi(servico.Object);
        using var cliente = fabrica.CreateClient();

        cliente.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                GeradorDeTokenJwt.GerarComAssinaturaInvalida());

        var resultado = await cliente.GetAsync("/api/lancamentos/1");

        resultado.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetComTokenExpiradoDeveRetornar401()
    {
        var servico = new Mock<ILancamentoServico>();

        using var fabrica = new FabricaDaApi(servico.Object);
        using var cliente = fabrica.CreateClient();

        cliente.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                GeradorDeTokenJwt.GerarExpirado());

        var resultado = await cliente.GetAsync("/api/lancamentos/1");

        resultado.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetComIssuerJwtInvalidoDeveRetornar401()
    {
        var servico = new Mock<ILancamentoServico>();

        using var fabrica = new FabricaDaApi(servico.Object);
        using var cliente = fabrica.CreateClient();

        cliente.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                GeradorDeTokenJwt.GerarComIssuerInvalido());

        var resultado = await cliente.GetAsync("/api/lancamentos/1");

        resultado.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetComAudienceJwtInvalidaDeveRetornar401()
    {
        var servico = new Mock<ILancamentoServico>();

        using var fabrica = new FabricaDaApi(servico.Object);
        using var cliente = fabrica.CreateClient();

        cliente.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                GeradorDeTokenJwt.GerarComAudienceInvalida());

        var resultado = await cliente.GetAsync("/api/lancamentos/1");

        resultado.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetComTokenAindaNaoValidoDeveRetornar401()
    {
        var servico = new Mock<ILancamentoServico>();

        using var fabrica = new FabricaDaApi(servico.Object);
        using var cliente = fabrica.CreateClient();

        cliente.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                GeradorDeTokenJwt.GerarAindaNaoValido());

        var resultado = await cliente.GetAsync("/api/lancamentos/1");

        resultado.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public void ChaveJwtMenorQue256BitsDeveImpedirInicializacao()
    {
        const string nomeVariavel = "Jwt__Chave";

        var valorAnterior =
            Environment.GetEnvironmentVariable(nomeVariavel);

        try
        {
            Environment.SetEnvironmentVariable(
                nomeVariavel,
                "chave-curta");

            var servico = new Mock<ILancamentoServico>();

            using var fabrica =
                new FabricaDaApi(servico.Object);

            var acao = () => fabrica.CreateClient();

            acao.Should()
                .Throw<InvalidOperationException>()
                .WithMessage("*pelo menos 256 bits*");
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                nomeVariavel,
                valorAnterior);
        }
    }

    [Fact]
    public void AudienceJwtNaoConfiguradaDeveImpedirInicializacao()
    {
        const string nomeVariavel = "Jwt__Audience";

        var valorAnterior =
            Environment.GetEnvironmentVariable(nomeVariavel);

        try
        {
            Environment.SetEnvironmentVariable(
                nomeVariavel,
                string.Empty);

            var servico = new Mock<ILancamentoServico>();

            using var fabrica =
                new FabricaDaApi(servico.Object);

            var acao = () => fabrica.CreateClient();

            acao.Should()
                .Throw<InvalidOperationException>()
                .WithMessage("*Audience JWT não foi configurado*");
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                nomeVariavel,
                valorAnterior);
        }
    }

    [Fact]
    public async Task GetComTokenExpiradoRecentementeDeveRetornar401()
    {
        var servico = new Mock<ILancamentoServico>();

        using var fabrica = new FabricaDaApi(servico.Object);
        using var cliente = fabrica.CreateClient();

        cliente.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                GeradorDeTokenJwt.GerarExpiradoRecentemente());

        var resultado =
            await cliente.GetAsync("/api/lancamentos/1");

        resultado.StatusCode.Should()
            .Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetComAlgoritmoJwtNaoPermitidoDeveRetornar401()
    {
        var servico = new Mock<ILancamentoServico>();

        using var fabrica = new FabricaDaApi(servico.Object);
        using var cliente = fabrica.CreateClient();

        cliente.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                GeradorDeTokenJwt.GerarComAlgoritmoHs384());

        var resultado =
            await cliente.GetAsync("/api/lancamentos/1");

        resultado.StatusCode.Should()
            .Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetComTokenJwtSemAssinaturaDeveRetornar401()
    {
        var servico = new Mock<ILancamentoServico>();

        using var fabrica = new FabricaDaApi(servico.Object);
        using var cliente = fabrica.CreateClient();

        cliente.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                GeradorDeTokenJwt.GerarSemAssinatura());

        var resultado =
            await cliente.GetAsync("/api/lancamentos/1");

        resultado.StatusCode.Should()
            .Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetComTokenJwtSemExpiracaoDeveRetornar401()
    {
        var servico = new Mock<ILancamentoServico>();

        using var fabrica = new FabricaDaApi(servico.Object);
        using var cliente = fabrica.CreateClient();

        cliente.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                GeradorDeTokenJwt.GerarSemExpiracao());

        var resultado =
            await cliente.GetAsync("/api/lancamentos/1");

        resultado.StatusCode.Should()
            .Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public void IssuerJwtNaoConfiguradoDeveImpedirInicializacao()
    {
        const string nomeVariavel = "Jwt__Issuer";

        var valorAnterior =
            Environment.GetEnvironmentVariable(nomeVariavel);

        try
        {
            Environment.SetEnvironmentVariable(
                nomeVariavel,
                string.Empty);

            var servico = new Mock<ILancamentoServico>();

            using var fabrica =
                new FabricaDaApi(servico.Object);

            var acao = () => fabrica.CreateClient();

            acao.Should()
                .Throw<InvalidOperationException>()
                .WithMessage("*Issuer JWT não foi configurado*");
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                nomeVariavel,
                valorAnterior);
        }
    }

    [Fact]
    public async Task GetComJwtSemIdentificacaoDoUsuarioDeveRetornar403()
    {
        var servico = new Mock<ILancamentoServico>();

        using var fabrica = new FabricaDaApi(servico.Object);
        using var cliente = fabrica.CreateClient();

        cliente.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                GeradorDeTokenJwt.GerarSemIdentificacaoDoUsuario());

        var resultado =
            await cliente.GetAsync("/api/lancamentos/1");

        resultado.StatusCode.Should()
            .Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetComIdentificacaoDoUsuarioVaziaDeveRetornar403()
    {
        var servico = new Mock<ILancamentoServico>();

        using var fabrica = new FabricaDaApi(servico.Object);
        using var cliente = fabrica.CreateClient();

        cliente.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                GeradorDeTokenJwt.GerarComIdentificacaoDoUsuarioVazia());

        var resultado =
            await cliente.GetAsync("/api/lancamentos/1");

        resultado.StatusCode.Should()
            .Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetComIdentificacaoDoUsuarioEmBrancoDeveRetornar403()
    {
        var servico = new Mock<ILancamentoServico>();

        using var fabrica = new FabricaDaApi(servico.Object);
        using var cliente = fabrica.CreateClient();

        cliente.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                GeradorDeTokenJwt.GerarComIdentificacaoDoUsuarioEmBranco());

        var resultado =
            await cliente.GetAsync("/api/lancamentos/1");

        resultado.StatusCode.Should()
            .Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostComJwtSemIdentificacaoDoUsuarioDeveRetornar403()
    {
        var servico = new Mock<ILancamentoServico>();

        using var fabrica = new FabricaDaApi(servico.Object);
        using var cliente = fabrica.CreateClient();

        cliente.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                GeradorDeTokenJwt.GerarSemIdentificacaoDoUsuario());

        var resultado = await cliente.PostAsJsonAsync(
            "/api/lancamentos",
            CriarRequisicao());

        resultado.StatusCode.Should()
            .Be(HttpStatusCode.Forbidden);

        servico.Verify(
            x => x.CriarAsync(
                It.IsAny<CriarLancamentoRequisicao>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PostSemTokenDeveRetornar401ENaoExecutarServico()
    {
        var servico = new Mock<ILancamentoServico>();

        using var fabrica = new FabricaDaApi(servico.Object);
        using var cliente = fabrica.CreateClient();

        var resultado = await cliente.PostAsJsonAsync(
            "/api/lancamentos",
            CriarRequisicao());

        resultado.StatusCode.Should()
            .Be(HttpStatusCode.Unauthorized);

        servico.Verify(
            x => x.CriarAsync(
                It.IsAny<CriarLancamentoRequisicao>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PostComTokenInvalidoDeveRetornar401ENaoExecutarServico()
    {
        var servico = new Mock<ILancamentoServico>();

        using var fabrica = new FabricaDaApi(servico.Object);
        using var cliente = fabrica.CreateClient();

        cliente.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                "token-jwt-invalido");

        var resultado = await cliente.PostAsJsonAsync(
            "/api/lancamentos",
            CriarRequisicao());

        resultado.StatusCode.Should()
            .Be(HttpStatusCode.Unauthorized);

        servico.Verify(
            x => x.CriarAsync(
                It.IsAny<CriarLancamentoRequisicao>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void ChaveJwtNaoConfiguradaDeveImpedirInicializacao()
    {
        const string nomeVariavel = "Jwt__Chave";

        var valorAnterior =
            Environment.GetEnvironmentVariable(nomeVariavel);

        try
        {
            Environment.SetEnvironmentVariable(
                nomeVariavel,
                string.Empty);

            var servico = new Mock<ILancamentoServico>();

            using var fabrica =
                new FabricaDaApi(servico.Object);

            var acao = () => fabrica.CreateClient();

            acao.Should()
                .Throw<InvalidOperationException>()
                .WithMessage("*chave JWT não foi configurada*");
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                nomeVariavel,
                valorAnterior);
        }
    }

    private static HttpClient CriarClienteAutenticado(
        WebApplicationFactory<Program> fabrica)
    {
        var cliente = fabrica.CreateClient();

        cliente.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                GeradorDeTokenJwt.Gerar());

        return cliente;
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

    private static LancamentoResposta CriarResposta(
        CriarLancamentoRequisicao requisicao)
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

    private sealed class FabricaDaApi(
        ILancamentoServico servico)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(
            IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration(
                (_, configuracao) =>
                {
                    configuracao.Sources.Clear();

                    configuracao.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["ConnectionStrings:LivroRazao"] =
                                $"Host=localhost;Port=5432;Database=teste;Username={Guid.NewGuid():N};Password={Guid.NewGuid():N}",

                            ["RabbitMq:Host"] = "localhost",
                            ["RabbitMq:Porta"] = "5672",
                            ["RabbitMq:Exchange"] = "teste",
                            ["RabbitMq:Usuario"] = "usuario-teste",
                            ["RabbitMq:Senha"] = "senha-teste",
                            ["RabbitMq:RoutingKey"] = "teste",
                            ["RabbitMq:Fila"] = "teste",

                            ["PublicadorOutbox:IntervaloEmSegundos"] = "1",
                            ["PublicadorOutbox:IntervaloDeAlertaDeTentativas"] = "1",
                            ["PublicadorOutbox:TempoLimiteDeProcessamentoEmMinutos"] = "1",

                            ["Jwt:Issuer"] = "FluxoDeCaixa.Testes",
                            ["Jwt:Audience"] = "FluxoDeCaixa.Testes.Api",
                            ["Jwt:Chave"] = "FluxoDeCaixa-Desenvolvimento-Chave-JWT-2026-Segura"
                        });
                }
                );

            builder.ConfigureTestServices(
                services =>
                {
                    services.RemoveAll<ILancamentoServico>();
                    services.AddSingleton(servico);

                    services.RemoveAll<IInicializadorRabbitMq>();
                    services.AddSingleton(
                        Mock.Of<IInicializadorRabbitMq>());

                    var servicosHospedados = services
                        .Where(
                            x => x.ServiceType ==
                                 typeof(IHostedService))
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
