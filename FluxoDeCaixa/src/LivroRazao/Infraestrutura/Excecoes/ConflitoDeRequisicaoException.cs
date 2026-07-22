namespace LivroRazao.Infraestrutura.Excecoes;

public sealed class ConflitoDeRequisicaoException(
    string mensagem) : Exception(mensagem);
