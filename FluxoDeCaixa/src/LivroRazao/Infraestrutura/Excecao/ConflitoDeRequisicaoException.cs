namespace LivroRazao.Infraestrutura.Excecao;

public sealed class ConflitoDeRequisicaoException(
    string mensagem) : Exception(mensagem);
