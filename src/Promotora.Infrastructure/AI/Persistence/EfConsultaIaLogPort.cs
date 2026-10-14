using System.Text.Json;
using Promotora.Application.AI.Consultas;
using Promotora.Infrastructure.Shared.Persistence;

namespace Promotora.Infrastructure.AI.Persistence;

public sealed class EfConsultaIaLogPort(PromotoraDbContext db) : IConsultaIaLogPort
{
    public async Task GuardarAsync(ConsultaRegistrada consulta, CancellationToken ct)
    {
        db.LogsAgenteIa.Add(new LogAgenteIaRow
        {
            UsuarioId = null,
            SessionId = consulta.SessionId,
            Consulta = consulta.Consulta,
            Respuesta = consulta.Respuesta,
            FuentesCitadas = JsonSerializer.SerializeToDocument(consulta.Fuentes, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            Estado = consulta.Estado,
            Modelo = consulta.Modelo,
            TokensEntrada = consulta.TokensEntrada,
            TokensSalida = consulta.TokensSalida,
            TiempoRespuestaMs = consulta.TiempoRespuestaMs,
            FechaHora = consulta.FechaHora
        });
        await db.SaveChangesAsync(ct);
    }
}
