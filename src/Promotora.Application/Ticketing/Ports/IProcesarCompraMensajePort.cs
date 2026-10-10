using Promotora.Domain.Shared;

namespace Promotora.Application.Ticketing.Ports;

public sealed record MensajeCompraDto(long MensajeId, Guid PublicId);

public interface IProcesarCompraMensajePort
{
    // Success solo tras commit de efectos + mensajes_procesados; duplicados/estados ya tomados también se omiten con éxito.
    // El integrador financiero de F7 implementará este contrato; no se registra un no-op de producción.
    Task<Result<Unit>> ProcesarAsync(MensajeCompraDto mensaje, CancellationToken ct);
}

public interface ICompraMessageConsumer
{
    Task EjecutarAsync(CancellationToken ct);
}
