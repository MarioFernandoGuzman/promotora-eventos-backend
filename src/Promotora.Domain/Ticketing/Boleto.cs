using System.Text.Json;
using Promotora.Domain.Shared;

namespace Promotora.Domain.Ticketing;

public sealed record DatosAsistente(string? NombreAsistente, string? DocumentoIdentidad, string? DatosExtraJson = null);

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
        var datos = asistente ?? new DatosAsistente(null, null);
        string? nombre = Normalizar(datos.NombreAsistente), documento = Normalizar(datos.DocumentoIdentidad);
        if (esCarrera && (nombre is null || documento is null))
            return Result<Boleto>.Failure(new Error("boleto.asistente", "Carreras requieren nombre y documento por asistente.", ErrorType.BusinessRule));
        try
        {
            if (datos.DatosExtraJson is not null)
            {
                using var extra = JsonDocument.Parse(datos.DatosExtraJson);
            }
        }
        catch (JsonException)
        {
            return Result<Boleto>.Failure(new Error("boleto.extra", "Los datos extra deben ser JSON válido.", ErrorType.Validation));
        }
        return Result<Boleto>.Success(new(publicId, detalleCompraId, codigoQr, new(nombre, documento, datos.DatosExtraJson), fechaEmision));
    }

    private static string? Normalizar(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
