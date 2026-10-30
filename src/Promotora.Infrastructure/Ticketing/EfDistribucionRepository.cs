using Microsoft.EntityFrameworkCore;
using Npgsql;
using Promotora.Application.Shared.Ports;
using Promotora.Application.Ticketing.Numeracion;
using Promotora.Domain.Shared;
using Promotora.Infrastructure.Shared.Persistence;
using Promotora.Infrastructure.Ticketing.Persistence;

namespace Promotora.Infrastructure.Ticketing;

public sealed class EfDistribucionRepository(PromotoraDbContext db, IClock clock) : IDistribucionRepository
{
    public async Task<ContextoDistribucion?> ContextoAsync(int eventoId, int localidadId, bool bloquear, CancellationToken ct)
    {
        var inicial = await db.Eventos.AsNoTracking().SingleOrDefaultAsync(e => e.Id == eventoId, ct);
        if (inicial is null) return null;
        if (bloquear)
        {
            if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Configuración requiere transacción.");
            await db.Recintos.FromSqlInterpolated($"SELECT * FROM recintos WHERE id={inicial.RecintoId} FOR UPDATE").AsNoTracking().ToListAsync(ct);
            await db.Eventos.FromSqlInterpolated($"SELECT * FROM eventos WHERE id={eventoId} FOR UPDATE").AsNoTracking().ToListAsync(ct);
            await db.Localidades.FromSqlInterpolated($"SELECT * FROM localidades WHERE id={localidadId} FOR UPDATE").AsNoTracking().ToListAsync(ct);
            await db.TiposEntrada.FromSqlInterpolated($"SELECT * FROM tipos_entrada WHERE evento_id={eventoId} AND localidad_id={localidadId} ORDER BY id FOR UPDATE").AsNoTracking().ToListAsync(ct);
            await db.DistribucionesEvento.FromSqlInterpolated($"SELECT * FROM distribuciones_evento WHERE evento_id={eventoId} AND localidad_id={localidadId} FOR UPDATE").AsNoTracking().ToListAsync(ct);
        }
        return await (from e in db.Eventos
                      join r in db.Recintos on e.RecintoId equals r.Id
                      join l in db.Localidades on r.Id equals l.RecintoId
                      join c in db.CategoriasEvento on e.CategoriaId equals c.Id
                      where e.Id == eventoId && l.Id == localidadId && e.RecintoId == inicial.RecintoId
                      select new ContextoDistribucion(r.DuenoId, r.Activo && l.Activo && c.Activo && e.Estado != "CANCELADO" && e.Estado != "FINALIZADO",
                          r.Activo && l.Activo && c.Activo && (e.Estado == "PROGRAMADO" || e.Estado == "EN_VENTA" || e.Estado == "AGOTADO"))).SingleOrDefaultAsync(ct);
    }

    public async Task<DistribucionDto?> ObtenerAsync(int eventoId, int localidadId, bool publico, int page, int pageSize, long? mesaId, CancellationToken ct)
    {
        var d = await db.DistribucionesEvento.AsNoTracking().SingleOrDefaultAsync(d => d.EventoId == eventoId && d.LocalidadId == localidadId && (!publico || d.Estado == "PUBLICADA"), ct);
        if (d is null) return null;
        var mesas = db.MesasEvento.AsNoTracking().Where(m => m.DistribucionId == d.Id && (mesaId == null || m.Id == mesaId));
        var asientos = db.AsientosEvento.AsNoTracking().Where(a => a.DistribucionId == d.Id && (mesaId == null || a.MesaId == mesaId));
        int totalMesas = await mesas.CountAsync(ct), totalAsientos = await asientos.CountAsync(ct);
        var filasMesa = await mesas.OrderBy(m => m.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(m => new MesaPlanoDto(m.Id, m.Etiqueta, m.Modalidad, m.TipoEntradaId, m.X, m.Y, db.AsientosEvento.Count(a => a.MesaId == m.Id))).ToArrayAsync(ct);
        var filasAsiento = await asientos.OrderBy(a => a.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => new AsientoPlanoDto(a.Id, a.MesaId, a.Etiqueta, a.Fila, a.TipoEntradaId, a.X, a.Y, a.Estado)).ToArrayAsync(ct);
        return new(d.Id, eventoId, localidadId, d.Estado, d.ImagenPlanoUrl, page, pageSize, totalMesas, totalAsientos, filasMesa, filasAsiento);
    }

    public async Task<Result<DistribucionDto>> GuardarAsync(GuardarDistribucionCommand command, CancellationToken ct)
    {
        var datos = command.Datos;
        var d = await db.DistribucionesEvento.SingleOrDefaultAsync(d => d.EventoId == command.EventoId && d.LocalidadId == command.LocalidadId, ct);
        if (d is not null && d.Estado != "BORRADOR") return Fallo("La distribución publicada es inmutable.");
        var mesas = d is null ? [] : await db.MesasEvento.Where(m => m.DistribucionId == d.Id).ToListAsync(ct);
        var sillas = d is null ? [] : await db.AsientosEvento.Where(a => a.DistribucionId == d.Id).ToListAsync(ct);
        if (datos.Mesas.Select(m => m.Clave).Distinct().Count() != datos.Mesas.Count ||
            datos.Mesas.Select(m => m.Etiqueta.Trim()).Distinct().Count() != datos.Mesas.Count ||
            datos.Asientos.Select(a => a.Etiqueta.Trim()).Distinct().Count() != datos.Asientos.Count ||
            datos.Mesas.Where(m => m.Id.HasValue).Select(m => m.Id).Distinct().Count() != mesas.Count ||
            datos.Asientos.Where(a => a.Id.HasValue).Select(a => a.Id).Distinct().Count() != sillas.Count ||
            mesas.Any(m => datos.Mesas.Count(x => x.Id == m.Id) != 1) || sillas.Any(a => datos.Asientos.Count(x => x.Id == a.Id) != 1))
            return Fallo("Claves o etiquetas repetidas; incluye todos los IDs existentes sin posiciones ajenas.");
        var tipos = await db.TiposEntrada.Where(t => t.EventoId == command.EventoId && t.LocalidadId == command.LocalidadId && t.Activo).Select(t => t.Id).ToArrayAsync(ct);
        var capacidad = await db.Localidades.Where(l => l.Id == command.LocalidadId).Select(l => l.Capacidad).SingleAsync(ct);
        if (datos.Asientos.Count > capacidad || datos.Mesas.Any(m => !tipos.Contains(m.TipoEntradaId)) || datos.Asientos.Any(a => !tipos.Contains(a.TipoEntradaId)))
            return Fallo("La distribución supera capacidad o usa tarifas ajenas/inactivas.");
        var porClave = datos.Mesas.ToDictionary(m => m.Clave);
        if (datos.Asientos.Any(a => a.MesaClave is not null && (!porClave.TryGetValue(a.MesaClave, out var m) || m.TipoEntradaId != a.TipoEntradaId)))
            return Fallo("La silla debe coincidir con mesa y tarifa.");
        if (await db.DetalleCompra.AnyAsync(dc => db.TiposEntrada.Any(t => t.Id == dc.TipoEntradaId && t.EventoId == command.EventoId && t.LocalidadId == command.LocalidadId), ct))
            return Fallo("No se convierte una localidad con historial de compras.");
        d ??= new DistribucionEventoRow { EventoId = command.EventoId, LocalidadId = command.LocalidadId, FechaCreacion = clock.UtcNow };
        if (d.Id == 0) db.DistribucionesEvento.Add(d);
        d.ImagenPlanoUrl = datos.ImagenPlanoUrl;
        await db.SaveChangesAsync(ct);
        // Las etiquetas temporales evitan conflictos al intercambiar dos nombres existentes.
        foreach (var m in mesas) m.Etiqueta = Guid.NewGuid().ToString("N");
        foreach (var a in sillas) a.Etiqueta = Guid.NewGuid().ToString("N");
        await db.SaveChangesAsync(ct);
        var mapa = new Dictionary<string, MesaEventoRow>();
        foreach (var config in datos.Mesas)
        {
            var m = config.Id is long id ? mesas.Single(m => m.Id == id) : new MesaEventoRow { DistribucionId = d.Id };
            if (m.Id == 0) db.MesasEvento.Add(m);
            m.Etiqueta = config.Etiqueta.Trim(); m.Modalidad = config.Modalidad; m.TipoEntradaId = config.TipoEntradaId; m.X = config.X; m.Y = config.Y;
            mapa.Add(config.Clave, m);
        }
        // Mesa con FK compuesta: no se cambia tarifa una vez referenciada por sillas.
        if (mesas.Any(m => sillas.Any(a => a.MesaId == m.Id && a.TipoEntradaId != m.TipoEntradaId)))
            return Fallo("Una mesa existente con sillas conserva su tipo de entrada; crea otra mesa para otra tarifa.");
        await db.SaveChangesAsync(ct);
        foreach (var config in datos.Asientos)
        {
            var a = config.Id is long id ? sillas.Single(a => a.Id == id) : new AsientoEventoRow { DistribucionId = d.Id };
            if (a.Id == 0) db.AsientosEvento.Add(a);
            a.MesaId = config.MesaClave is null ? null : mapa[config.MesaClave].Id;
            a.Etiqueta = config.Etiqueta.Trim(); a.Fila = config.Fila; a.TipoEntradaId = config.TipoEntradaId;
            a.X = config.X; a.Y = config.Y; a.Estado = config.Bloqueado ? "BLOQUEADO" : "DISPONIBLE";
        }
        await db.SaveChangesAsync(ct);
        return Result<DistribucionDto>.Success((await ObtenerAsync(command.EventoId, command.LocalidadId, false, 1, 100, null, ct))!);
    }

    public async Task<Result<DistribucionDto>> PublicarAsync(PublicarDistribucionCommand command, CancellationToken ct)
    {
        var d = await db.DistribucionesEvento.AsNoTracking().SingleOrDefaultAsync(d => d.EventoId == command.EventoId && d.LocalidadId == command.LocalidadId, ct);
        if (d is null) return Result<DistribucionDto>.Failure(DistribucionAccess.NoEncontrada);
        if (d.Estado == "PUBLICADA") return Result<DistribucionDto>.Success((await ObtenerAsync(command.EventoId, command.LocalidadId, false, 1, 100, null, ct))!);
        var sillas = await db.AsientosEvento.Where(a => a.DistribucionId == d.Id).ToArrayAsync(ct);
        var mesas = await db.MesasEvento.Where(m => m.DistribucionId == d.Id).ToArrayAsync(ct);
        int maximo = await db.Eventos.Where(e => e.Id == command.EventoId).Select(e => e.MaxBoletosPorCompra).SingleAsync(ct);
        if (sillas.Length == 0 || mesas.Any(m => !sillas.Any(a => a.MesaId == m.Id) ||
            m.Modalidad == "COMPLETA" && (sillas.Count(a => a.MesaId == m.Id) > maximo || sillas.Where(a => a.MesaId == m.Id).Select(a => a.Estado).Distinct().Count() != 1)))
            return Fallo("Mesas vacías, bloqueo parcial o capacidad de mesa superior al máximo por compra.");
        var tipos = await db.TiposEntrada.Where(t => t.EventoId == command.EventoId && t.LocalidadId == command.LocalidadId).ToArrayAsync(ct);
        if (tipos.Any(t => !t.Activo || !sillas.Any(a => a.TipoEntradaId == t.Id)) ||
            await db.DetalleCompra.AnyAsync(dc => tipos.Select(t => t.Id).Contains(dc.TipoEntradaId), ct))
            return Fallo("Publicar requiere todos los tipos activos, con sillas y sin historial de compras.");
        foreach (var t in tipos)
        {
            t.CupoTotal = sillas.Count(a => a.TipoEntradaId == t.Id);
            t.CupoDisponible = sillas.Count(a => a.TipoEntradaId == t.Id && a.Estado == "DISPONIBLE");
        }
        await db.SaveChangesAsync(ct);
        await db.DistribucionesEvento.Where(x => x.Id == d.Id && x.Estado == "BORRADOR")
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Estado, "PUBLICADA").SetProperty(x => x.FechaPublicacion, clock.UtcNow), ct);
        return Result<DistribucionDto>.Success((await ObtenerAsync(command.EventoId, command.LocalidadId, false, 1, 100, null, ct))!);
    }

    public async Task<Result<Unit>> BloquearAsync(BloquearAsientosCommand command, CancellationToken ct)
    {
        var d = await db.DistribucionesEvento.AsNoTracking().SingleOrDefaultAsync(d => d.EventoId == command.EventoId && d.LocalidadId == command.LocalidadId && d.Estado == "PUBLICADA", ct);
        if (d is null) return Result<Unit>.Failure(DistribucionAccess.NoEncontrada);
        var ids = command.AsientoIds.ToArray();
        var sillas = await db.AsientosEvento.AsNoTracking().Where(a => a.DistribucionId == d.Id && ids.Contains(a.Id)).ToArrayAsync(ct);
        if (sillas.Length != ids.Length) return Result<Unit>.Failure(DistribucionAccess.NoEncontrada);
        string esperado = command.Bloquear ? "DISPONIBLE" : "BLOQUEADO", siguiente = command.Bloquear ? "BLOQUEADO" : "DISPONIBLE";
        if (sillas.Any(a => a.Estado != esperado)) return Result<Unit>.Failure(Conflicto("Hay posiciones reservadas, vendidas o en otro estado."));
        var mesas = await db.MesasEvento.Where(m => m.DistribucionId == d.Id && m.Modalidad == "COMPLETA" && sillas.Select(a => a.MesaId).Contains(m.Id)).Select(m => m.Id).ToArrayAsync(ct);
        if (await db.AsientosEvento.AnyAsync(a => a.MesaId != null && mesas.Contains(a.MesaId.Value) && !ids.Contains(a.Id), ct))
            return Result<Unit>.Failure(Conflicto("Bloquea o desbloquea todas las sillas de la mesa completa."));
        int filas = await db.AsientosEvento.Where(a => ids.Contains(a.Id) && a.DistribucionId == d.Id && a.Estado == esperado)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.Estado, siguiente), ct);
        if (filas != ids.Length) return Result<Unit>.Failure(Conflicto("La disponibilidad cambió."));
        foreach (var grupo in sillas.GroupBy(a => a.TipoEntradaId).OrderBy(g => g.Key))
        {
            int cambio = command.Bloquear ? -grupo.Count() : grupo.Count();
            await db.TiposEntrada.Where(t => t.Id == grupo.Key).ExecuteUpdateAsync(s => s.SetProperty(t => t.CupoDisponible, t => t.CupoDisponible + cambio), ct);
        }
        return Result<Unit>.Success(Unit.Value);
    }

    private static Error Conflicto(string mensaje) => new("numeracion.conflicto", mensaje, ErrorType.Conflict);
    private static Result<DistribucionDto> Fallo(string mensaje) => Result<DistribucionDto>.Failure(Conflicto(mensaje));
}
