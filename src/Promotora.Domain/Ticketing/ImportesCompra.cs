using Promotora.Domain.Finance;
using Promotora.Domain.Shared;

namespace Promotora.Domain.Ticketing;

public sealed record ImportesCompra
{
    public const decimal Maximo = 99999999.99m; // NUMERIC(10,2) de Flyway.
    public decimal Subtotal { get; }
    public decimal ComisionPlataforma { get; }
    public decimal Impuestos { get; }
    public decimal Total { get; }
    public bool RequiereMovimientos => Total > 0;
    private ImportesCompra(decimal subtotal, decimal comision, decimal impuestos, decimal total)
        => (Subtotal, ComisionPlataforma, Impuestos, Total) = (subtotal, comision, impuestos, total);

    public static Result<ImportesCompra> Calcular(decimal subtotal, PoliticaTarifaria politica)
    {
        ArgumentNullException.ThrowIfNull(politica);
        if (!MontoValido(subtotal) || politica.TasaIva < 0 || politica.ComisionPorcentaje < 0)
            return Result<ImportesCompra>.Failure(Invalido);
        try
        {
            decimal comision = decimal.Round(subtotal * politica.ComisionPorcentaje, 2, MidpointRounding.AwayFromZero);
            decimal impuestos = decimal.Round((subtotal + comision) * politica.TasaIva, 2, MidpointRounding.AwayFromZero);
            return DesdeMontos(subtotal, comision, impuestos, subtotal + comision + impuestos);
        }
        catch (OverflowException) { return Result<ImportesCompra>.Failure(Invalido); }
    }

    // Rehidratar importes congelados: nunca recalcular con la política vigente.
    public static Result<ImportesCompra> DesdeMontos(decimal subtotal, decimal comision, decimal impuestos, decimal total)
        => MontoValido(subtotal) && MontoValido(comision) && MontoValido(impuestos) && MontoValido(total)
            && subtotal + comision + impuestos == total
            ? Result<ImportesCompra>.Success(new(subtotal, comision, impuestos, total))
            : Result<ImportesCompra>.Failure(Invalido);

    internal static bool MontoValido(decimal monto) => monto >= 0 && monto <= Maximo && decimal.Round(monto, 2) == monto;
    private static readonly Error Invalido = new("ticketing.importes", "Importes o tasas inválidos, o total fuera del límite monetario.", ErrorType.Validation);
}
