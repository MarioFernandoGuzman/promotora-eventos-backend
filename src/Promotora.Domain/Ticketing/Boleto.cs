using System.Text.Json;
using Promotora.Domain.Shared;

namespace Promotora.Domain.Ticketing;

public sealed record DatosAsistente(string? NombreAsistente, string? DocumentoIdentidad, string? DatosExtraJson = null)
{
    public Result<DatosAsistente> Validar(bool esCarrera)
    {
        string? nombre = string.IsNullOrWhiteSpace(NombreAsistente) ? null : NombreAsistente.Trim();
        string? documento = string.IsNullOrWhiteSpace(DocumentoIdentidad) ? null : DocumentoIdentidad.Trim();
        if (esCarrera && (nombre is null || documento is null))
            return Result<DatosAsistente>.Failure(new Error("boleto.asistente", "Carreras requieren nombre y documento por asistente.", ErrorType.BusinessRule));
        try
        {
            if (DatosExtraJson is not null) { using var extra = JsonDocument.Parse(DatosExtraJson); }
        }
        catch (JsonException)
        {
            return Result<DatosAsistente>.Failure(new Error("boleto.extra", "Los datos extra deben ser JSON válido.", ErrorType.Validation));
        }
        return Result<DatosAsistente>.Success(new(nombre, documento, DatosExtraJson));
    }
}

public sealed record Boleto
{
    public Guid PublicId { get; }
    public int DetalleCompraId { get; }
    public string CodigoQr { get; }
    public string Estado => "VALIDO";
    public DatosAsistente Asistente { get; }
    public DateTime FechaEmision { get; }

    private Boleto(Guid publicId, int detalleCompraId, string codigoQr, DatosAsistente asistente, DateTime fechaEmision)
    {
        PublicId = publicId; DetalleCompraId = detalleCompraId; CodigoQr = codigoQr; Asistente = asistente; FechaEmision = fechaEmision;
    }

    public static Result<Boleto> Crear(Guid publicId, int detalleCompraId, string codigoQr, DatosAsistente? asistente, bool esCarrera, DateTime fechaEmision)
    {
        if (publicId == Guid.Empty || detalleCompraId <= 0 || string.IsNullOrWhiteSpace(codigoQr) || codigoQr.Length > 100 || fechaEmision.Kind != DateTimeKind.Utc)
            return Result<Boleto>.Failure(new Error("boleto.datos", "Datos de emisión inválidos.", ErrorType.Validation));
        var datos = (asistente ?? new DatosAsistente(null, null)).Validar(esCarrera);
        if (datos.IsFailure) return Result<Boleto>.Failure(datos.Errors);
        return Result<Boleto>.Success(new(publicId, detalleCompraId, codigoQr, datos.Value, fechaEmision));
    }

}
