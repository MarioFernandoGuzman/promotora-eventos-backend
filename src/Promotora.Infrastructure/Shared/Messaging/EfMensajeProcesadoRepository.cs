using Microsoft.EntityFrameworkCore;
using Promotora.Application.Shared.Ports;
using Promotora.Infrastructure.Shared.Persistence;

namespace Promotora.Infrastructure.Shared.Messaging;

public sealed class EfMensajeProcesadoRepository(PromotoraDbContext db, IClock clock) : IMensajeProcesadoRepository
{
    public async Task<bool> RegistrarAsync(long mensajeId, string consumidor, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("La deduplicación requiere la transacción de efectos activa.");
        if (mensajeId <= 0 || string.IsNullOrWhiteSpace(consumidor) || consumidor.Length > 100)
            throw new ArgumentException("Identidad de mensaje o consumidor inválida.");
        var fecha = clock.UtcNow;
        var filas = await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO public.mensajes_procesados (mensaje_id, consumidor, fecha) VALUES ({mensajeId}, {consumidor}, {fecha}) ON CONFLICT (mensaje_id, consumidor) DO NOTHING", ct);
        return filas == 1;
    }
}
