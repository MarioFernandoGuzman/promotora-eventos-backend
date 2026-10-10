using Promotora.Domain.Shared;

namespace Promotora.Domain.Venues;

public sealed class Localidad
{
    public int Id { get; }
    public int RecintoId { get; }
    public string Nombre { get; private set; }
    public int Capacidad { get; private set; }
    public Poligono? Geometria { get; private set; }
    public string? Color { get; private set; }
    public bool Activo { get; }

    public Localidad(int id, int recintoId, string nombre, int capacidad, Poligono? geometria, string? color, bool activo)
    {
        if (recintoId <= 0 || string.IsNullOrWhiteSpace(nombre) || nombre.Length > 100 || capacidad <= 0 || !ColorValido(color))
        {
            throw new ArgumentException("Datos de localidad inválidos.");
        }

        Id = id;
        RecintoId = recintoId;
        Nombre = nombre;
        Capacidad = capacidad;
        Geometria = geometria;
        Color = color;
        Activo = activo;
    }

    public static bool ColorValido(string? color) => color is null || (color.Length == 7 && color[0] == '#'
        && color.Skip(1).All(x => char.IsAsciiHexDigit(x)));

    public static Result<Unit> ValidarCapacidad(int capacidad, int? capacidadRecinto, long capacidadOtrasLocalidades, long cupoPorEvento)
    {
        if (capacidad <= 0 || capacidadRecinto is null || capacidadOtrasLocalidades < 0
            || capacidadOtrasLocalidades + capacidad > capacidadRecinto || capacidad < cupoPorEvento)
        {
            return Result<Unit>.Failure(new Error("venues.capacidad", "La capacidad debe respetar el recinto y los cupos asignados por evento.", ErrorType.BusinessRule));
        }

        return Result<Unit>.Success(Unit.Value);
    }

    public Result<Unit> Editar(string nombre, int capacidad, Poligono? geometria, string? color,
        int? capacidadRecinto, long capacidadOtrasLocalidades, long cupoPorEvento)
    {
        if (!Activo)
        {
            return Result<Unit>.Failure(new Error("venues.localidad_inactiva", "La localidad está inactiva.", ErrorType.Conflict));
        }

        var valid = ValidarCapacidad(capacidad, capacidadRecinto, capacidadOtrasLocalidades, cupoPorEvento);
        if (valid.IsFailure)
        {
            return valid;
        }

        if (string.IsNullOrWhiteSpace(nombre) || nombre.Length > 100 || !ColorValido(color))
        {
            return Result<Unit>.Failure(new Error("venues.localidad", "Datos de localidad inválidos.", ErrorType.Validation));
        }

        Nombre = nombre;
        Capacidad = capacidad;
        Geometria = geometria;
        Color = color;
        return Result<Unit>.Success(Unit.Value);
    }
}
