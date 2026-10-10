namespace Promotora.Domain.Finance;

// Tasas fraccionarias recibidas desde Application; Domain no lee configuración.
public sealed record PoliticaTarifaria(decimal TasaIva, decimal ComisionPorcentaje);
