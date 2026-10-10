using Promotora.Domain.Finance;
using Promotora.Domain.Shared;

namespace Promotora.Domain.Ticketing;

public enum EstadoCompra { PENDIENTE, PROCESANDO, COMPLETADA, CANCELADA, FALLIDA, EXPIRADA }

public sealed class Compra
{
    public Guid PublicId { get; }
    public int ClienteId { get; }
    public int EventoId { get; }
    public DateTime FechaCompra { get; }
    public DateTime ExpiraEn { get; }
    public DateTime? ProcesandoDesde { get; private set; }
    public string IdempotencyKey { get; }
    public string Moneda { get; }
    public EstadoCompra Estado { get; private set; } = EstadoCompra.PENDIENTE;
    public IReadOnlyList<DetalleCompra> Detalles { get; }
    public ImportesCompra Importes { get; }

    private Compra(Guid publicId, int clienteId, int eventoId, DateTime fechaCompra, DateTime expiraEn,
        string idempotencyKey, string moneda, IReadOnlyList<DetalleCompra> detalles, ImportesCompra importes)
    {
        PublicId = publicId; ClienteId = clienteId; EventoId = eventoId; FechaCompra = fechaCompra; ExpiraEn = expiraEn;
        IdempotencyKey = idempotencyKey; Moneda = moneda; Detalles = detalles; Importes = importes;
    }

    public static Result<Compra> Crear(Guid publicId, int clienteId, int eventoId, DateTime ahora, DateTime expiraEn,
        string idempotencyKey, string moneda, IEnumerable<EntradaCompra> entradas, int maxBoletosPorCompra, PoliticaTarifaria politica)
    {
        ArgumentNullException.ThrowIfNull(entradas);
        if (publicId == Guid.Empty || clienteId <= 0 || eventoId <= 0 || ahora.Kind != DateTimeKind.Utc
            || expiraEn.Kind != DateTimeKind.Utc || expiraEn <= ahora || string.IsNullOrWhiteSpace(idempotencyKey)
            || idempotencyKey.Length > 100 || string.IsNullOrWhiteSpace(moneda) || moneda.Length != 3 || maxBoletosPorCompra <= 0)
            return Result<Compra>.Failure(Invalida);
        var detalles = new List<DetalleCompra>();
        long cantidad = 0;
        decimal subtotal = 0;
        foreach (var entrada in entradas)
        {
            if (entrada is null || entrada.EventoId != eventoId)
                return Result<Compra>.Failure(new Error("ticketing.evento", "Todas las entradas deben pertenecer al evento de la compra.", ErrorType.BusinessRule));
            var detalle = DetalleCompra.Crear(entrada.TipoEntradaId, entrada.Cantidad, entrada.PrecioUnitario);
            if (detalle.IsFailure) return Result<Compra>.Failure(detalle.Errors);
            cantidad += entrada.Cantidad;
            if (cantidad > maxBoletosPorCompra)
                return Result<Compra>.Failure(new Error("ticketing.limite", "Se excede el máximo de boletos por compra.", ErrorType.BusinessRule));
            subtotal += detalle.Value.Subtotal;
            if (subtotal > ImportesCompra.Maximo) return Result<Compra>.Failure(Invalida);
            detalles.Add(detalle.Value);
        }
        if (detalles.Count == 0) return Result<Compra>.Failure(Invalida);
        var importes = ImportesCompra.Calcular(subtotal, politica);
        return importes.IsFailure ? Result<Compra>.Failure(importes.Errors) : Result<Compra>.Success(new(publicId, clienteId, eventoId,
            ahora, expiraEn, idempotencyKey, moneda, detalles.AsReadOnly(), importes.Value));
    }

    // En persistencia el adaptador debe repetir el predicado WHERE estado=esperado.
    // Este método no reserva ni devuelve cupo; eso corresponde a F5-02 y su transacción.
    public Result<Unit> CambiarEstado(EstadoCompra esperado, EstadoCompra nuevo, DateTime ahora)
    {
        if (Estado != esperado) return Result<Unit>.Failure(new Error("ticketing.estado", "El estado esperado ya cambió.", ErrorType.Conflict));
        if (!Enum.IsDefined(nuevo) || ahora.Kind != DateTimeKind.Utc || ahora < FechaCompra || (ProcesandoDesde is { } inicio && ahora < inicio))
            return Result<Unit>.Failure(Invalida);
        bool permitido = (Estado, nuevo) switch
        {
            (EstadoCompra.PENDIENTE, EstadoCompra.PROCESANDO or EstadoCompra.CANCELADA or EstadoCompra.EXPIRADA) => true,
            (EstadoCompra.PROCESANDO, EstadoCompra.COMPLETADA or EstadoCompra.FALLIDA) => true,
            _ => false
        };
        if (!permitido) return Result<Unit>.Failure(new Error("ticketing.transicion", "Transición de compra no permitida.", ErrorType.Conflict));
        if (nuevo == EstadoCompra.EXPIRADA && ahora < ExpiraEn)
            return Result<Unit>.Failure(new Error("ticketing.expiracion", "La reserva aún no ha vencido.", ErrorType.BusinessRule));
        if (nuevo == EstadoCompra.PROCESANDO) ProcesandoDesde = ahora;
        Estado = nuevo;
        return Result<Unit>.Success(Unit.Value);
    }
    private static readonly Error Invalida = new("ticketing.compra", "Datos de compra inválidos.", ErrorType.Validation);
}
