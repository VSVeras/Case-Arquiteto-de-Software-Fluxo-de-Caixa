using LivroRazao.Infraestrutura.RegistroDeEvento;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace LivroRazao.TesteDeUnidade.Infraestrutura;

public sealed class RegistroDeEventoTestes
{
    [Fact]
    public void InformacaoDeveRegistrarLogInformation()
    {
        var logger = new Mock<ILogger<RegistroDeEvento>>();
        var registro = new RegistroDeEvento(logger.Object);

        registro.Informacao("Evento {Codigo}.", 10);

        VerificarLog(logger, LogLevel.Information, null, "Evento 10.");
    }

    [Fact]
    public void AvisoDeveRegistrarLogWarningComExcecao()
    {
        var logger = new Mock<ILogger<RegistroDeEvento>>();
        var registro = new RegistroDeEvento(logger.Object);
        var excecao = new InvalidOperationException("Falha de aviso.");

        registro.Aviso(excecao, "Aviso {Codigo}.", 20);

        VerificarLog(logger, LogLevel.Warning, excecao, "Aviso 20.");
    }

    [Fact]
    public void ErroDeveRegistrarLogErrorComExcecao()
    {
        var logger = new Mock<ILogger<RegistroDeEvento>>();
        var registro = new RegistroDeEvento(logger.Object);
        var excecao = new InvalidOperationException("Falha de erro.");

        registro.Erro(excecao, "Erro {Codigo}.", 30);

        VerificarLog(logger, LogLevel.Error, excecao, "Erro 30.");
    }

    private static void VerificarLog(
        Mock<ILogger<RegistroDeEvento>> logger,
        LogLevel nivel,
        Exception? excecao,
        string mensagemEsperada)
    {
        logger.Verify(
            x => x.Log(
                nivel,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((estado, _) =>
                    estado.ToString() == mensagemEsperada),
                excecao,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
