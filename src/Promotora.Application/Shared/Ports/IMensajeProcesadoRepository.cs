namespace Promotora.Application.Shared.Ports;

public interface IMensajeProcesadoRepository
{
    // Requiere la misma transacción que los efectos del consumidor. False = duplicado confirmado.
    Task<bool> RegistrarAsync(long mensajeId, string consumidor, CancellationToken ct);
}
