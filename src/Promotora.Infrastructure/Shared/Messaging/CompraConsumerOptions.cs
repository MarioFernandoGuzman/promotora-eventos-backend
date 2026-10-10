namespace Promotora.Infrastructure.Shared.Messaging;

public sealed record CompraConsumerOptions(ushort Prefetch = 16, int MaxIntentos = 3, int RetryBaseDelayMilliseconds = 5000)
{
    public void Validar()
    {
        if (Prefetch is < 1 or > 1000 || MaxIntentos is < 1 or > 10 || RetryBaseDelayMilliseconds is < 1 or > 300000)
            throw new InvalidOperationException("Consumidor requiere prefetch 1–1000, intentos 1–10 y retraso 1–300000 ms.");
    }

    public int Retraso(int intentoFallido) => (int)Math.Min(300000L, RetryBaseDelayMilliseconds * (1L << Math.Clamp(intentoFallido - 1, 0, 9)));
}
