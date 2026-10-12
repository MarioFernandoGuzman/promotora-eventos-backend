using Promotora.Domain.Shared;
using Promotora.Domain.Ticketing;

namespace Promotora.Domain.Finance;

public sealed record MovimientoOriginal(long Id, int CuentaId, string Tipo, decimal Monto);

public static class MovimientosReembolso
{
    public static Result<IReadOnlyList<MovimientoCobro>> Invertir(ImportesCompra importes, string moneda,
        IReadOnlyList<MovimientoOriginal> originales, IReadOnlyList<CuentaCobro> cuentas)
    {
        var plan = new List<MovimientoCobro>();
        foreach (var (tipo, tipoCuenta, monto) in new[] { ("VENTA", "DUENO", importes.Subtotal),
            ("COMISION", "PLATAFORMA", importes.ComisionPlataforma), ("IMPUESTO", "IMPUESTOS", importes.Impuestos) })
        {
            var filas = originales.Where(m => m.Tipo == tipo).ToArray();
            if (monto == 0m) { if (filas.Length != 0) return Fallo(); continue; }
            if (filas.Length != 1 || filas[0].Monto != monto || filas[0].Id <= 0) return Fallo();
            var original = filas[0];
            var candidatas = cuentas.Where(c => c.Id == original.CuentaId).ToArray();
            if (candidatas.Length != 1 || candidatas[0].Tipo != tipoCuenta || candidatas[0].Moneda != moneda) return Fallo();
            plan.Add(new(original.CuentaId, "REEMBOLSO", -original.Monto));
        }
        if (originales.Count != plan.Count || plan.Select(m => m.CuentaId).Distinct().Count() != plan.Count
            || originales.Select(m => m.Id).Distinct().Count() != originales.Count || plan.Sum(m => m.Monto) != -importes.Total) return Fallo();
        return Result<IReadOnlyList<MovimientoCobro>>.Success(plan);
    }

    public static Result<Unit> ValidarDisponibilidad(IReadOnlyList<MovimientoCobro> plan, IReadOnlyList<CuentaCobro> cuentas)
    {
        foreach (var movimiento in plan)
        {
            var cuenta = cuentas.Single(c => c.Id == movimiento.CuentaId);
            if (cuenta.Estado != "ACTIVA" || cuenta.Saldo < -movimiento.Monto)
                return Result<Unit>.Failure(new Error("reembolso.saldo", "Una cuenta está cerrada o no tiene saldo suficiente.", ErrorType.Conflict));
        }
        return Result<Unit>.Success(Unit.Value);
    }

    private static Result<IReadOnlyList<MovimientoCobro>> Fallo() => Result<IReadOnlyList<MovimientoCobro>>.Failure(
        new Error("reembolso.respaldo", "Los movimientos originales no corresponden a los importes congelados.", ErrorType.Conflict));
}
