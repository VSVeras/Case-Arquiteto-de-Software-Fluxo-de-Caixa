using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace LivroRazao.Infraestrutura.Mensageria;

public sealed class RabbitMqConexao : IRabbitMqConexao
{
    private readonly ConnectionFactory _fabricaDeConexao;
    private readonly object _sincronizacao = new();

    private IConnection? _conexao;
    private bool _descartado;

    public RabbitMqConexao(IOptions<RabbitMqConfiguracao> opcoes)
    {
        var configuracao = opcoes.Value;

        _fabricaDeConexao = new ConnectionFactory
        {
            HostName = configuracao.Host,
            Port = configuracao.Porta,
            UserName = configuracao.Usuario,
            Password = configuracao.Senha,
            VirtualHost = configuracao.VirtualHost,
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true
        };
    }

    public IConnection ObterConexao()
    {
        ObjectDisposedException.ThrowIf(_descartado, this);
        if (_conexao is { IsOpen: true })
        {
            return _conexao;
        }

        lock (_sincronizacao)
        {
            ObjectDisposedException.ThrowIf(_descartado, this);
            if (_conexao is { IsOpen: true })
            {
                return _conexao;
            }

            DescartarConexaoAtual();

            _conexao = _fabricaDeConexao.CreateConnection();

            return _conexao;
        }
    }

    public void Dispose()
    {
        lock (_sincronizacao)
        {
            if (_descartado)
            {
                return;
            }

            _descartado = true;

            DescartarConexaoAtual();
        }
    }

    private void DescartarConexaoAtual()
    {
        if (_conexao is null)
        {
            return;
        }

        try
        {
            if (_conexao.IsOpen)
            {
                _conexao.Close();
            }
        }
        finally
        {
            _conexao.Dispose();
            _conexao = null;
        }
    }
}
