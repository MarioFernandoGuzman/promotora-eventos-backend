using Promotora.Domain.Shared;

namespace Promotora.Domain.Catalog;

public enum EstadoEvento { BORRADOR, PROGRAMADO, EN_VENTA, AGOTADO, FINALIZADO, CANCELADO }

public sealed record DatosEvento(int RecintoId, int CategoriaId, string Nombre, string? Descripcion, DateTime FechaInicio,
    DateTime? FechaFin, DateTime? FechaInicioVenta, DateTime? FechaFinVenta, int MaxBoletosPorCompra, string? ImagenUrl);

public sealed class Evento
{
    public int Id { get; }
    public int CreadoPor { get; }
    public DatosEvento Datos { get; private set; }
    public EstadoEvento Estado { get; private set; }
    public DateTime FechaCreacion { get; }
    public bool AdmiteConfiguracion => Estado is EstadoEvento.BORRADOR or EstadoEvento.PROGRAMADO;

    public Evento(int id, int creadoPor, DatosEvento datos, EstadoEvento estado, DateTime fechaCreacion)
    {
        if (creadoPor <= 0 || !Enum.IsDefined(estado) || fechaCreacion.Kind != DateTimeKind.Utc || !DatosValidos(datos))
            throw new ArgumentException("Datos de evento inválidos.");
        Id = id;
        CreadoPor = creadoPor;
        Datos = datos;
        Estado = estado;
        FechaCreacion = fechaCreacion;
    }

    public Result<Unit> Editar(DatosEvento datos)
    {
        if (!AdmiteConfiguracion) return Result<Unit>.Failure(new Error("catalog.estado", "Solo se configura un evento BORRADOR o PROGRAMADO.", ErrorType.Conflict));
        if (datos.RecintoId != Datos.RecintoId) return Result<Unit>.Failure(new Error("catalog.recinto", "No se traslada el evento a otro recinto.", ErrorType.BusinessRule));
        if (!DatosValidos(datos)) return Result<Unit>.Failure(new Error("catalog.fechas", "Datos o fechas de evento inválidos.", ErrorType.Validation));
        Datos = datos;
        return Result<Unit>.Success(Unit.Value);
    }

    public Result<Unit> CambiarEstado(EstadoEvento esperado, EstadoEvento nuevo, DateTime ahora, long cupoDisponible)
    {
        if (Estado != esperado) return Result<Unit>.Failure(new Error("catalog.estado_concurrente", "El estado esperado ya cambió.", ErrorType.Conflict));
        bool permitido = (Estado, nuevo) switch
        {
            (EstadoEvento.BORRADOR, EstadoEvento.PROGRAMADO or EstadoEvento.CANCELADO) => true,
            (EstadoEvento.PROGRAMADO, EstadoEvento.EN_VENTA or EstadoEvento.CANCELADO) => true,
            (EstadoEvento.EN_VENTA, EstadoEvento.AGOTADO or EstadoEvento.FINALIZADO or EstadoEvento.CANCELADO) => true,
            (EstadoEvento.AGOTADO, EstadoEvento.EN_VENTA or EstadoEvento.FINALIZADO or EstadoEvento.CANCELADO) => true,
            _ => false
        };
        if (!permitido) return Result<Unit>.Failure(new Error("catalog.transicion", "Transición de evento no permitida.", ErrorType.BusinessRule));
        if (nuevo == EstadoEvento.EN_VENTA && (Datos.FechaInicioVenta is null || Datos.FechaFinVenta is null
            || ahora < Datos.FechaInicioVenta || ahora >= Datos.FechaFinVenta || cupoDisponible <= 0))
            return Result<Unit>.Failure(new Error("catalog.ventana", "Abrir venta requiere ventana vigente y cupo disponible.", ErrorType.BusinessRule));
        if (nuevo == EstadoEvento.AGOTADO && cupoDisponible != 0)
            return Result<Unit>.Failure(new Error("catalog.cupo", "El evento aún tiene cupo disponible.", ErrorType.BusinessRule));
        if (nuevo == EstadoEvento.FINALIZADO && ahora < (Datos.FechaFin ?? Datos.FechaInicio))
            return Result<Unit>.Failure(new Error("catalog.fecha_fin", "El evento todavía no ha finalizado.", ErrorType.BusinessRule));
        Estado = nuevo;
        return Result<Unit>.Success(Unit.Value);
    }

    private static bool DatosValidos(DatosEvento datos) => datos.RecintoId > 0 && datos.CategoriaId > 0
        && !string.IsNullOrWhiteSpace(datos.Nombre) && datos.Nombre.Length <= 150 && datos.MaxBoletosPorCompra > 0
        && datos.FechaInicio.Kind == DateTimeKind.Utc && Utc(datos.FechaFin) && Utc(datos.FechaInicioVenta) && Utc(datos.FechaFinVenta)
        && (datos.FechaFin is null || datos.FechaFin >= datos.FechaInicio)
        && (datos.FechaInicioVenta is null || datos.FechaFinVenta is null || datos.FechaFinVenta >= datos.FechaInicioVenta)
        && (datos.ImagenUrl is null || datos.ImagenUrl.Length <= 255);

    private static bool Utc(DateTime? date) => date is null || date.Value.Kind == DateTimeKind.Utc;
}
