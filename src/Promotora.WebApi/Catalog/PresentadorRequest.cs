using Promotora.Domain.Catalog;

namespace Promotora.WebApi.Catalog;

public sealed record PresentadorRequest(string Nombre, string Tipo, string? Biografia, string? ImagenUrl)
{
    public DatosPresentador ToDomain() => new(Nombre, Tipo, Biografia, ImagenUrl);
}
