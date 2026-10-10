using Promotora.Domain.Shared;

namespace Promotora.Domain.Venues;

public sealed class Recinto
{
    public int Id { get; }
    public int DuenoId { get; }
    public DatosRecinto Datos { get; private set; }
    public bool Activo { get; }
    public DateTime FechaCreacion { get; }

    public Recinto(int id, int duenoId, DatosRecinto datos, bool activo, DateTime fechaCreacion)
    {
        if (duenoId <= 0 || fechaCreacion.Kind != DateTimeKind.Utc || !DatosValidos(datos))
        {
            throw new ArgumentException("Datos de recinto inválidos.");
        }

        Id = id;
        DuenoId = duenoId;
        Datos = datos;
        Activo = activo;
        FechaCreacion = fechaCreacion;
    }

    public Result<Unit> Editar(DatosRecinto datos, long capacidadLocalidades, bool tieneLocalidades)
    {
        if (!Activo)
        {
            return Result<Unit>.Failure(new Error("venues.inactivo", "El recinto está inactivo.", ErrorType.Conflict));
        }

        if (!DatosValidos(datos) || capacidadLocalidades < 0 || (tieneLocalidades
            && (datos.CapacidadTotal is null || capacidadLocalidades > datos.CapacidadTotal)))
        {
            return Result<Unit>.Failure(new Error("venues.capacidad", "La capacidad debe cubrir todas las localidades, incluidas las inactivas.", ErrorType.BusinessRule));
        }

        Datos = datos;
        return Result<Unit>.Success(Unit.Value);
    }

    private static bool DatosValidos(DatosRecinto datos) => !string.IsNullOrWhiteSpace(datos.Nombre)
        && datos.Nombre.Length <= 150 && (datos.Direccion is null || datos.Direccion.Length <= 255)
        && (datos.Ciudad is null || datos.Ciudad.Length <= 100) && datos.CapacidadTotal is not <= 0
        && datos.Latitud is not (< -90 or > 90) && datos.Longitud is not (< -180 or > 180)
        && (datos.ImagenPlanoUrl is null || datos.ImagenPlanoUrl.Length <= 255);
}
