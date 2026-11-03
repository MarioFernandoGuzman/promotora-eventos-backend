using Promotora.Domain.Shared;

namespace Promotora.Domain.Catalog;

public sealed record DatosPresentador(string Nombre, string Tipo, string? Biografia, string? ImagenUrl);

public sealed class Presentador
{
    public int Id { get; }
    public DatosPresentador Datos { get; private set; }
    public bool Activo { get; }

    public Presentador(int id, DatosPresentador datos, bool activo)
    {
        if (!Validos(datos)) throw new ArgumentException("Datos de catálogo inválidos.");
        Id = id;
        Datos = datos;
        Activo = activo;
    }

    public Result<Unit> Editar(DatosPresentador datos)
    {
        if (!Activo) return Result<Unit>.Failure(new Error("catalog.inactivo", "El recurso está inactivo.", ErrorType.Conflict));
        if (!Validos(datos)) return Result<Unit>.Failure(new Error("catalog.datos", "Datos de catálogo inválidos.", ErrorType.Validation));
        Datos = datos;
        return Result<Unit>.Success(Unit.Value);
    }

    private static bool Validos(DatosPresentador datos) => !string.IsNullOrWhiteSpace(datos.Nombre) && datos.Nombre.Length <= 150 && datos.Tipo is ("ARTISTA" or "EQUIPO" or "PRESENTADOR" or "ORGANIZACION") && (datos.ImagenUrl is null || datos.ImagenUrl.Length <= 255);
}
