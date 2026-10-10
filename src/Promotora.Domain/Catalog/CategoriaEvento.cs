using Promotora.Domain.Shared;

namespace Promotora.Domain.Catalog;

public sealed record DatosCategoriaEvento(string Nombre, string? Descripcion);

public sealed class CategoriaEvento
{
    public int Id { get; }
    public DatosCategoriaEvento Datos { get; private set; }
    public bool Activo { get; }

    public CategoriaEvento(int id, DatosCategoriaEvento datos, bool activo)
    {
        if (!Validos(datos)) throw new ArgumentException("Datos de catálogo inválidos.");
        Id = id;
        Datos = datos;
        Activo = activo;
    }

    public Result<Unit> Editar(DatosCategoriaEvento datos)
    {
        if (!Activo) return Result<Unit>.Failure(new Error("catalog.inactivo", "El recurso está inactivo.", ErrorType.Conflict));
        if (!Validos(datos)) return Result<Unit>.Failure(new Error("catalog.datos", "Datos de catálogo inválidos.", ErrorType.Validation));
        Datos = datos;
        return Result<Unit>.Success(Unit.Value);
    }

    private static bool Validos(DatosCategoriaEvento datos) => !string.IsNullOrWhiteSpace(datos.Nombre) && datos.Nombre.Length <= 50 && (datos.Descripcion is null || datos.Descripcion.Length <= 255);
}
