using Promotora.Domain.Catalog;

namespace Promotora.WebApi.Catalog;

public sealed record CategoriaEventoRequest(string Nombre, string? Descripcion)
{
    public DatosCategoriaEvento ToDomain() => new(Nombre, Descripcion);
}
