using Promotora.Domain.Shared;

namespace Promotora.Domain.Finance;

public sealed class CuentaFinanciera(int id, string tipo, int? duenoId, string moneda, decimal saldo, string estado, DateTime? fechaCierre)
{
    public int Id { get; } = id;
    public string Tipo { get; } = tipo;
    public int? DuenoId { get; } = duenoId;
    public string Moneda { get; } = moneda;
    public decimal Saldo { get; } = saldo;
    public string Estado { get; private set; } = estado;
    public DateTime? FechaCierre { get; private set; } = fechaCierre;

    public Result<decimal> Liquidar(decimal monto)
    {
        if (monto <= 0m || monto > 9999999999.99m || decimal.Round(monto, 2) != monto)
            return Result<decimal>.Failure(new Error("liquidacion.monto", "El monto debe ser positivo, con máximo dos decimales y dentro del rango monetario.", ErrorType.Validation));
        if (Tipo != "DUENO" || DuenoId is null || Estado != "ACTIVA")
            return Result<decimal>.Failure(new Error("liquidacion.cuenta", "Solo se liquidan cuentas de dueño activas.", ErrorType.Conflict));
        if (Saldo < monto)
            return Result<decimal>.Failure(new Error("liquidacion.saldo", "Saldo insuficiente para liquidar.", ErrorType.Conflict));
        return Result<decimal>.Success(-monto);
    }

    public Result<Unit> Cerrar(DateTime fecha)
    {
        if (fecha.Kind != DateTimeKind.Utc) return Result<Unit>.Failure(new Error("cuenta.fecha", "La fecha debe ser UTC.", ErrorType.Validation));
        if (Tipo != "DUENO" || DuenoId is null)
            return Result<Unit>.Failure(new Error("cuenta.tipo", "Solo se cierran cuentas de dueños.", ErrorType.Conflict));
        if (Saldo != 0m) return Result<Unit>.Failure(new Error("cuenta.saldo", "La cuenta debe tener saldo cero.", ErrorType.Conflict));
        if (Estado == "CERRADA") return Result<Unit>.Success(Unit.Value);
        if (Estado != "ACTIVA") return Result<Unit>.Failure(new Error("cuenta.estado", "Estado de cuenta inválido.", ErrorType.Conflict));
        Estado = "CERRADA"; FechaCierre = fecha;
        return Result<Unit>.Success(Unit.Value);
    }
}
