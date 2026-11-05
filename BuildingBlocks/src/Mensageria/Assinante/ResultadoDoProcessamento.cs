namespace Mensageria.Assinante;
public sealed record ResultadoDoProcessamento(TipoResultadoProcessamento Tipo, Exception? Excecao = null)
{
    public static ResultadoDoProcessamento Sucesso() => new(TipoResultadoProcessamento.Sucesso);
    public static ResultadoDoProcessamento ErroTransitorio(Exception? excecao = null) => new(TipoResultadoProcessamento.ErroTransitorio, excecao);
    public static ResultadoDoProcessamento ErroDefinitivo(Exception? excecao = null) => new(TipoResultadoProcessamento.ErroDefinitivo, excecao);
}
