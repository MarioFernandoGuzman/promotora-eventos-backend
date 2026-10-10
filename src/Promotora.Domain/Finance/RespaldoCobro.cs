namespace Promotora.Domain.Finance;

// Instantánea del registro financiero; no recalcula las tasas congeladas.
public sealed record RespaldoCobro(bool PagoAprobado, bool CuentasCorrectas, bool SoloCobrosPositivos,
    long CantidadMovimientos, decimal Venta, decimal Comision, decimal Impuesto)
{
    public bool PermiteEmision(Ticketing.ImportesCompra importes) => CuentasCorrectas && SoloCobrosPositivos
        && Venta == importes.Subtotal && Comision == importes.ComisionPlataforma && Impuesto == importes.Impuestos
        && CantidadMovimientos == (importes.Subtotal > 0m ? 1 : 0) + (importes.ComisionPlataforma > 0m ? 1 : 0) + (importes.Impuestos > 0m ? 1 : 0)
        && (importes.Total == 0m || PagoAprobado);
}
