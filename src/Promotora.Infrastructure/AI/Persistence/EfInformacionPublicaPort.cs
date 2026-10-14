using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Promotora.Application.AI.Consultas;
using Promotora.Application.AI.Rag;
using Promotora.Application.Shared.Ports;
using Promotora.Infrastructure.Shared.Persistence;

namespace Promotora.Infrastructure.AI.Persistence;

public sealed class EfInformacionPublicaPort(PromotoraDbContext db, IClock reloj) : IInformacionPublicaPort
{
    public async Task<IReadOnlyList<FuenteContexto>> BuscarAsync(string pregunta, CancellationToken ct)
    {
        string[] palabras = Regex.Matches(pregunta.ToLowerInvariant(), @"[\p{L}\p{N}]{3,}", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))
            .Select(m => m.Value).Except(new[] { "qué", "que", "como", "cómo", "cuánto", "cuanto", "cuándo", "cuando", "dónde", "donde", "hay", "los", "las", "del", "una", "para", "evento", "eventos", "precio", "precios", "entradas", "venta", "recinto", "fechas", "disponibles", "son", "cuesta", "cuestan" })
            .Distinct().Take(8).ToArray();
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
        DateTime ahora = reloj.UtcNow;
        var query = from e in db.Eventos.AsNoTracking()
                    join r in db.Recintos.AsNoTracking() on e.RecintoId equals r.Id
                    join c in db.CategoriasEvento.AsNoTracking() on e.CategoriaId equals c.Id
                    where e.Estado == "EN_VENTA" && r.Activo && c.Activo && e.FechaInicio > ahora
                        && (e.FechaInicioVenta == null || e.FechaInicioVenta <= ahora) && (e.FechaFinVenta == null || e.FechaFinVenta > ahora)
                    select new { e.Id, e.Nombre, e.FechaInicio, Recinto = r.Nombre, r.Ciudad, r.Direccion };
        if (palabras.Length != 0) query = query.Where(e => palabras.Any(p => e.Nombre.ToLower().Contains(p) || e.Recinto.ToLower().Contains(p) || (e.Ciudad != null && e.Ciudad.ToLower().Contains(p))));
        var eventos = await query.OrderBy(e => e.FechaInicio).ThenBy(e => e.Id).Take(5).ToArrayAsync(ct);
        var resultado = new List<FuenteContexto>();
        foreach (var evento in eventos)
        {
            var tipos = await db.TiposEntrada.AsNoTracking().Where(t => t.EventoId == evento.Id && t.Activo
                && (t.LocalidadId == null || db.Localidades.Any(l => l.Id == t.LocalidadId && l.Activo)))
                .OrderBy(t => t.Id).Select(t => new { t.Nombre, t.Precio }).Take(8).ToArrayAsync(ct);
            string precios = string.Join("; ", tipos.Select(t => $"{t.Nombre}: {t.Precio.ToString("0.00", CultureInfo.InvariantCulture)}"));
            string texto = $"Evento {evento.Nombre}. Fecha de inicio UTC: {evento.FechaInicio:O}. Recinto: {evento.Recinto}. Ciudad: {evento.Ciudad}. Dirección: {evento.Direccion}. Precios base sin IVA ni comisión: {precios}. El cupo y total definitivo se confirman al comprar; esta información no reserva entradas.";
            resultado.Add(new($"evento:{evento.Id}", evento.Nombre, null, null, null, $"/api/v1/catalogo/eventos/{evento.Id}", texto));
        }
        await tx.CommitAsync(ct);
        return resultado;
    }
}
