using Promotora.Domain.Catalog;

namespace Promotora.WebApi.Catalog;

public sealed record CrearTipoEntradaRequest(string Nombre, decimal Precio, int CupoTotal, int? LocalidadId = null)
{
    public DatosTipoEntrada ToDomain() => new(Nombre, Precio, CupoTotal);
}
public sealed record EditarTipoEntradaRequest(string Nombre, decimal Precio, int CupoTotal)
{
    public DatosTipoEntrada ToDomain() => new(Nombre, Precio, CupoTotal);
}
