using System.Collections;
namespace Mensageria.RabbitMQ.Retry;
public static class ContadorDeTentativasRabbitMq
{
    public static long Obter(IReadOnlyDictionary<string, object?>? headers)
    {
        if (headers is null || !headers.TryGetValue("x-death", out var valor) || valor is not IList xDeath) return 0;
        long maior = 0;
        foreach (var item in xDeath)
        {
            if (item is not IDictionary<string, object?> dados || !dados.TryGetValue("count", out var count)) continue;
            var numero = Converter(count);
            if (numero > maior) maior = numero;
        }
        return maior;
    }
    private static long Converter(object? valor) => valor switch { byte n => n, short n => n, int n => n, long n => n, uint n => n, ulong n when n <= long.MaxValue => (long)n, _ => 0 };
}
