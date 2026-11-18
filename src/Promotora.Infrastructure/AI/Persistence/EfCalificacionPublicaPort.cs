using System.Globalization;
using Promotora.Application.AI.Ports;
using Promotora.Application.AI.Rag;
using Promotora.Application.Reviews;

namespace Promotora.Infrastructure.AI.Persistence;

public sealed class EfCalificacionPublicaPort(IResenaRepository resenas) : ICalificacionPublicaPort
{
    public async Task<FuenteContexto?> ConsultarAsync(int presentadorId, CancellationToken ct)
    {
        var dato = await resenas.CalificacionAsync(presentadorId, ct);
        if (dato is null) return null;
        string texto = dato.Promedio is decimal promedio
            ? $"El presentador {presentadorId} tiene una calificación promedio de {promedio.ToString("0.00", CultureInfo.InvariantCulture)} sobre 5, basada en {dato.Cantidad} reseñas aprobadas."
            : $"El presentador {presentadorId} no tiene reseñas aprobadas; su calificación promedio no está disponible.";
        return new($"calificacion-presentador:{presentadorId}", "Calificación pública del presentador", null, null, null,
            $"/api/v1/presentadores/{presentadorId}/calificacion", texto);
    }
}
