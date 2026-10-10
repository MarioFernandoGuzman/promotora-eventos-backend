namespace Promotora.Worker.Messaging;

public sealed record OutboxRelayOptions(int BatchSize = 50, int PollIntervalSeconds = 2)
{
    public void Validar()
    {
        if (BatchSize is < 1 or > 1000 || PollIntervalSeconds is < 1 or > 300)
            throw new InvalidOperationException("Outbox requiere lote 1–1000 e intervalo 1–300 segundos.");
    }
}
