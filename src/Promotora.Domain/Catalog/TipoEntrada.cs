using Promotora.Domain.Shared;

namespace Promotora.Domain.Catalog;

public sealed record DatosTipoEntrada(string Nombre, decimal Precio, int CupoTotal);

public sealed class TipoEntrada
{
    public int Id { get; }
    public int EventoId { get; }
    public int? LocalidadId { get; }
    public DatosTipoEntrada Datos { get; private set; }
    public int CupoDisponible { get; private set; }
    public bool Activo { get; }

    public TipoEntrada(int id, int eventoId, int? localidadId, DatosTipoEntrada datos, int cupoDisponible, bool activo)
    {
        if (eventoId <= 0 || localidadId is <= 0 || !DatosValidos(datos) || cupoDisponible < 0 || cupoDisponible > datos.CupoTotal)
            throw new ArgumentException("Tipo de entrada inválido.");
        Id = id; EventoId = eventoId; LocalidadId = localidadId; Datos = datos; CupoDisponible = cupoDisponible; Activo = activo;
    }

    public Result<Unit> Editar(DatosTipoEntrada datos)
    {
        if (!Activo) return Result<Unit>.Failure(new Error("catalog.inactivo", "El tipo de entrada está inactivo.", ErrorType.Conflict));
        if (CupoDisponible != Datos.CupoTotal) return Result<Unit>.Failure(new Error("catalog.reservado", "No se configura un tipo con cupo reservado o vendido.", ErrorType.Conflict));
        if (!DatosValidos(datos)) return Result<Unit>.Failure(new Error("catalog.tipo", "Nombre, precio o cupo inválidos.", ErrorType.Validation));
        // La disponibilidad persistida se ajusta con UPDATE atómico en el adaptador, nunca desde el cuerpo HTTP.
        Datos = datos;
        CupoDisponible = datos.CupoTotal;
        return Result<Unit>.Success(Unit.Value);
    }

    public static Result<Unit> ValidarCapacidad(int cupo, long otrosCupos, int capacidad) => cupo < 0 || otrosCupos < 0 || (long)cupo + otrosCupos > capacidad
        ? Result<Unit>.Failure(new Error("catalog.capacidad", "Los cupos del evento superan la capacidad de la localidad.", ErrorType.BusinessRule))
        : Result<Unit>.Success(Unit.Value);

    private static bool DatosValidos(DatosTipoEntrada datos) => !string.IsNullOrWhiteSpace(datos.Nombre) && datos.Nombre.Length <= 100
        && datos.Precio >= 0 && datos.Precio <= 99999999.99m && decimal.Round(datos.Precio, 2) == datos.Precio && datos.CupoTotal >= 0;
}
