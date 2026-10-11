using Promotora.Domain.Shared;
using Promotora.Domain.Ticketing;

namespace Promotora.Domain.Finance;

public sealed record CuentaCobro(int Id, string Tipo, int? DuenoId, string Moneda, decimal Saldo, string Estado);
public sealed record MovimientoCobro(int CuentaId, string Tipo, decimal Monto);

public static class MovimientosCobro
{
    public static Result<IReadOnlyList<MovimientoCobro>> Crear(ImportesCompra importes, int duenoId, string moneda, IReadOnlyList<CuentaCobro> cuentas)
    {
        if (!importes.RequiereMovimientos) return Result<IReadOnlyList<MovimientoCobro>>.Success([]);
        var movimientos = new List<MovimientoCobro>();
        foreach (var (tipoCuenta, tipoMovimiento, monto) in new[] {
            ("DUENO", "VENTA", importes.Subtotal), ("PLATAFORMA", "COMISION", importes.ComisionPlataforma), ("IMPUESTOS", "IMPUESTO", importes.Impuestos) })
        {
            if (monto == 0m) continue;
            var candidatas = cuentas.Where(c => c.Tipo == tipoCuenta && c.Moneda == moneda
                && c.DuenoId == (tipoCuenta == "DUENO" ? duenoId : (int?)null)).ToArray();
            if (candidatas.Length != 1 || candidatas[0].Estado != "ACTIVA") return Fallo("finanzas.cuenta", "Falta una cuenta activa y única para el cobro.");
            var cuenta = candidatas[0];
            decimal saldoFinal = cuenta.Saldo + monto;
            if (saldoFinal is < -9999999999.99m or > 9999999999.99m) return Fallo("finanzas.saldo", "El saldo supera la precisión del esquema financiero.");
            movimientos.Add(new(cuenta.Id, tipoMovimiento, monto));
        }
        if (movimientos.Select(m => m.CuentaId).Distinct().Count() != movimientos.Count || movimientos.Sum(m => m.Monto) != importes.Total)
            return Fallo("finanzas.componentes", "Los movimientos no representan el total de la compra.");
        return Result<IReadOnlyList<MovimientoCobro>>.Success(movimientos);
    }

    private static Result<IReadOnlyList<MovimientoCobro>> Fallo(string codigo, string mensaje) =>
        Result<IReadOnlyList<MovimientoCobro>>.Failure(new Error(codigo, mensaje, ErrorType.Conflict));
}
