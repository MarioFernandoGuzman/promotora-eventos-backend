namespace Promotora.Worker.Ticketing;

public sealed record ExpiracionWorkerOptions(int BatchSize = 100, int PollIntervalSeconds = 30)
{
    public void Validar()
    {
        if (BatchSize is < 1 or > 1000 || PollIntervalSeconds is < 1 or > 300)
            throw new ArgumentOutOfRangeException(nameof(BatchSize), "Configuración de expiración fuera de rango.");
    }
}
