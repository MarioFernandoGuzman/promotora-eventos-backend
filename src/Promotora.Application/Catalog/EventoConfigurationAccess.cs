using Promotora.Application.Catalog.Ports;
using Promotora.Domain.Catalog;
using Promotora.Domain.Shared;

namespace Promotora.Application.Catalog;

public sealed class EventoConfigurationAccess(EventoAccess access, IEventoRepository eventos)
{
    public async Task<Result<Evento>> FindAsync(int id, CancellationToken ct)
    {
        var result = await access.FindAsync(id, true, true, ct);
        if (result.IsFailure) return result;
        if (!result.Value.AdmiteConfiguracion) return Result<Evento>.Failure(new Error("catalog.estado", "Solo se configura un evento BORRADOR o PROGRAMADO.", ErrorType.Conflict));
        if (await eventos.TieneComprasAsync(id, ct)) return Result<Evento>.Failure(new Error("catalog.compras", "No se configura un evento con historial de compras.", ErrorType.Conflict));
        return result;
    }
}
