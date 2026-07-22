namespace LivroRazao.Infraestrutura.Persistencia.Entidade.Outbox;

public enum StatusDaOutbox
{
    Pendente = 1,
    Processando = 2,
    Publicado = 3,
    Erro = 4
}
