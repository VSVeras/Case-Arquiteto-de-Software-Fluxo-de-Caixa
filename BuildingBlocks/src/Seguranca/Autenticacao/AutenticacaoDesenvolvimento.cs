using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Seguranca.Autorizacao;

namespace Seguranca.Autenticacao;

public sealed record AutenticacaoRequisicao(string Usuario, string Senha);

public sealed record AutenticacaoResposta(
    string AccessToken,
    string TokenType,
    int ExpiresIn);

public interface IAutenticacaoDesenvolvimento
{
    AutenticacaoResposta? Autenticar(AutenticacaoRequisicao requisicao);
}

internal sealed class AutenticacaoDesenvolvimento : IAutenticacaoDesenvolvimento
{
    private const int ExpiracaoEmSegundos = 3600;

    private readonly JwtConfiguracao _jwt;
    private readonly AutenticacaoDesenvolvimentoConfiguracao _autenticacao;

    public AutenticacaoDesenvolvimento(
        IOptions<JwtConfiguracao> jwt,
        IOptions<AutenticacaoDesenvolvimentoConfiguracao> autenticacao)
    {
        _jwt = jwt.Value;
        _autenticacao = autenticacao.Value;
    }

    public AutenticacaoResposta? Autenticar(AutenticacaoRequisicao requisicao)
    {
        if (!string.Equals(requisicao.Usuario, _autenticacao.Usuario, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(requisicao.Senha, _autenticacao.Senha, StringComparison.Ordinal))
        {
            return null;
        }

        var agora = DateTime.UtcNow;
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, _autenticacao.Usuario),
            new(ClaimTypes.Name, _autenticacao.Usuario)
        };

        claims.AddRange(AutorizacaoFuncional.Itens.Select(autorizacao =>
            new Claim(TiposDeClaim.Autorizacao, autorizacao.ToString())));

        var chave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Chave));
        var credenciais = new SigningCredentials(chave, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            notBefore: agora,
            expires: agora.AddSeconds(ExpiracaoEmSegundos),
            signingCredentials: credenciais);

        return new AutenticacaoResposta(
            new JwtSecurityTokenHandler().WriteToken(token),
            "Bearer",
            ExpiracaoEmSegundos);
    }
}
