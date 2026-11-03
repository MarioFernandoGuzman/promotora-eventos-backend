using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Promotora.Application.Audit;
using Promotora.Application.Finance.Ports;
using Promotora.Application.Shared.Ports;
using Promotora.Domain.Finance;
using Promotora.Domain.Shared;
using Promotora.Infrastructure.Finance.Persistence;
using Promotora.Infrastructure.Shared.Persistence;
using Promotora.Infrastructure.Ticketing.Persistence;

namespace Promotora.Infrastructure.Finance;

public sealed class EfRegistroPagoRepository(PromotoraDbContext db, IClock clock, IAuditLogger audit) : IRegistroPagoPort
{
    public async Task<Result<PagoRegistradoDto>> IniciarEnTransaccionAsync(Guid compraPublicId, string pasarela, CancellationToken ct)
    {
        ExigirTransaccion();
        if (compraPublicId == Guid.Empty || string.IsNullOrWhiteSpace(pasarela) || pasarela.Length > 30)
            return Fallo("pago.datos", "Referencia o pasarela inválidas.", ErrorType.Validation);
        var compra = await LockCompraAsync(compraPublicId, ct);
        if (compra is null) return Fallo("pago.compra", "Compra no encontrada.", ErrorType.NotFound);
        var existentes = await db.Pagos.AsNoTracking().Where(p => p.CompraId == compra.Id && (p.Estado == "PENDIENTE" || p.Estado == "APROBADO"))
            .OrderBy(p => p.Id).ToArrayAsync(ct);
        if (existentes.Length > 1) return Fallo("pago.inconsistente", "Hay más de un intento pendiente o aprobado.", ErrorType.Conflict);
        if (existentes.Length == 1)
        {
            var existente = existentes[0];
            if (existente.Pasarela != pasarela || existente.Referencia != compra.PublicId.ToString("D") || existente.Monto != compra.Total)
                return Fallo("pago.respuesta", "El intento existente no corresponde a esta solicitud.", ErrorType.Conflict);
            if (compra.Estado != "PROCESANDO" && !(existente.Estado == "APROBADO" && compra.Estado == "COMPLETADA"))
                return Fallo("pago.compra_estado", "La compra ya no admite iniciar o retomar este intento.", ErrorType.Conflict);
            return Result<PagoRegistradoDto>.Success(ToDto(existente, compra));
        }
        if (compra.Estado != "PROCESANDO") return Fallo("pago.compra_estado", "Solo se inicia un pago para una compra PROCESANDO.", ErrorType.Conflict);
        // Un pago reembolsado no admite otro cobro de la misma compra.
        if (await db.Pagos.AnyAsync(p => p.CompraId == compra.Id && p.Estado == "REEMBOLSADO", ct))
            return Fallo("pago.estado", "La compra ya tiene un pago reembolsado.", ErrorType.Conflict);
        var row = new PagoRow { CompraId = compra.Id, Pasarela = pasarela, Referencia = compra.PublicId.ToString("D"), Monto = compra.Total, Estado = "PENDIENTE", Fecha = clock.UtcNow };
        db.Pagos.Add(row); await db.SaveChangesAsync(ct);
        await audit.WriteAsync(null, "CREATE", "pagos", row.Id.ToString(CultureInfo.InvariantCulture), null, ct);
        var persistido = await db.Pagos.AsNoTracking().SingleAsync(p => p.Id == row.Id, ct);
        return Result<PagoRegistradoDto>.Success(ToDto(persistido, compra));
    }

    public async Task<Result<PagoRegistradoDto>> ResolverEnTransaccionAsync(long intentoId, RespuestaPago respuesta, CancellationToken ct)
    {
        ExigirTransaccion(); ArgumentNullException.ThrowIfNull(respuesta);
        var referencia = await (from p in db.Pagos
                                join c in db.Compras on p.CompraId equals c.Id
                                where p.Id == intentoId
                                select (Guid?)c.PublicId).SingleOrDefaultAsync(ct);
        if (referencia is null) return Fallo("pago.no_encontrado", "Intento no encontrado.", ErrorType.NotFound);
        var compra = (await LockCompraAsync(referencia.Value, ct))!;
        var row = await db.Pagos.AsNoTracking().SingleAsync(p => p.Id == intentoId, ct);
        if (row.Referencia != compra.PublicId.ToString("D") || row.Monto != compra.Total)
            return Fallo("pago.inconsistente", "El intento no conserva los importes de la compra.", ErrorType.Conflict);
        var pago = new Pago(compra.PublicId, compra.Total, compra.Moneda, row.Pasarela, row.Estado);
        var resultado = pago.Resolver(respuesta.Referencia, respuesta.Monto, respuesta.Moneda, respuesta.Pasarela, respuesta.Estado, respuesta.Fecha);
        if (resultado.IsFailure) return Result<PagoRegistradoDto>.Failure(resultado.Errors);
        if (row.Estado == pago.Estado) return Result<PagoRegistradoDto>.Success(ToDto(row, compra));
        if (compra.Estado != "PROCESANDO") return Fallo("pago.compra_estado", "La compra debe estar PROCESANDO.", ErrorType.Conflict);
        if (await db.Pagos.AnyAsync(p => p.CompraId == compra.Id && p.Id != intentoId && (p.Estado == "APROBADO" || p.Estado == "REEMBOLSADO"), ct))
            return Fallo("pago.inconsistente", "La compra ya tiene otro pago aprobado o reembolsado.", ErrorType.Conflict);
        // Solo campos permitidos; no persistir cuerpos crudos de una pasarela ni datos de tarjeta.
        using var json = JsonSerializer.SerializeToDocument(new { referencia = respuesta.Referencia, monto = respuesta.Monto, moneda = respuesta.Moneda, pasarela = respuesta.Pasarela, estado = respuesta.Estado, fecha = respuesta.Fecha });
        int filas = await db.Pagos.Where(p => p.Id == intentoId && p.Estado == "PENDIENTE")
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Estado, pago.Estado).SetProperty(p => p.Respuesta, json), ct);
        if (filas != 1) return Fallo("pago.estado", "El intento cambió de estado.", ErrorType.Conflict);
        // El intento insertado en este mismo contexto debe dejar de conservar PENDIENTE en el tracker.
        var tracked = db.ChangeTracker.Entries<PagoRow>().SingleOrDefault(e => e.Entity.Id == intentoId);
        if (tracked is not null) tracked.State = EntityState.Detached;
        await audit.WriteAsync(null, "PAYMENT", "pagos", intentoId.ToString(CultureInfo.InvariantCulture), null, ct);
        var persistido = await db.Pagos.AsNoTracking().SingleAsync(p => p.Id == intentoId, ct);
        return Result<PagoRegistradoDto>.Success(ToDto(persistido, compra));
    }

    private async Task<CompraRow?> LockCompraAsync(Guid publicId, CancellationToken ct) =>
        (await db.Compras.FromSqlInterpolated($"SELECT * FROM compras WHERE public_id={publicId} FOR UPDATE").AsNoTracking().ToListAsync(ct)).SingleOrDefault();
    private static PagoRegistradoDto ToDto(PagoRow p, CompraRow c) => new(p.Id, p.CompraId, c.PublicId, p.Monto, c.Moneda, p.Pasarela, p.Estado, p.Fecha);
    private static Result<PagoRegistradoDto> Fallo(string codigo, string mensaje, ErrorType tipo) => Result<PagoRegistradoDto>.Failure(new Error(codigo, mensaje, tipo));
    private void ExigirTransaccion()
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Registrar pagos requiere la transacción de compra.");
    }
}
