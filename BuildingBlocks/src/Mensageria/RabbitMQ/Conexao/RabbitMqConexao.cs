using Mensageria.RabbitMQ.Configuracao;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
namespace Mensageria.RabbitMQ.Conexao;
public sealed class RabbitMqConexao : IRabbitMqConexao
{
    private readonly ConnectionFactory _fabrica;
    private readonly SemaphoreSlim _sincronizacao = new(1, 1);
    private IConnection? _conexao;
    private bool _descartado;
    public RabbitMqConexao(IOptions<RabbitMqConfiguracao> opcoes)
    {
        var c = opcoes.Value;
        _fabrica = new ConnectionFactory { HostName = c.Host, Port = c.Porta, UserName = c.Usuario, Password = c.Senha, VirtualHost = c.VirtualHost, AutomaticRecoveryEnabled = true, TopologyRecoveryEnabled = true, ClientProvidedName = c.NomeDaConexao };
    }
    public async Task<IConnection> ObterConexaoAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_descartado, this);
        if (_conexao is { IsOpen: true }) return _conexao;
        await _sincronizacao.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(_descartado, this);
            if (_conexao is { IsOpen: true }) return _conexao;
            if (_conexao is not null) await _conexao.DisposeAsync();
            _conexao = await _fabrica.CreateConnectionAsync(cancellationToken);
            return _conexao;
        }
        finally { _sincronizacao.Release(); }
    }
    public async ValueTask DisposeAsync()
    {
        if (_descartado) return;
        _descartado = true;
        if (_conexao is not null) await _conexao.DisposeAsync();
        _sincronizacao.Dispose();
    }
}
