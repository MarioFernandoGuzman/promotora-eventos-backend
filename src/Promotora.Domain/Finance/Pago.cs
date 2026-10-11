using Promotora.Domain.Shared;

namespace Promotora.Domain.Finance;

public sealed class Pago(Guid referencia, decimal monto, string moneda, string pasarela, string estado)
{
    public string Estado { get; private set; } = estado;

    public Result<Unit> Resolver(Guid referenciaRespuesta, decimal montoRespuesta, string monedaRespuesta,
        string pasarelaRespuesta, string estadoRespuesta, DateTime fechaRespuesta)
    {
        if (referenciaRespuesta != referencia || montoRespuesta != monto || monedaRespuesta != moneda || pasarelaRespuesta != pasarela)
            return Error("pago.respuesta", "La respuesta no corresponde al intento y al importe congelado.");
        if (fechaRespuesta.Kind != DateTimeKind.Utc || estadoRespuesta is not ("APROBADO" or "RECHAZADO"))
            return Result<Unit>.Failure(new Error("pago.datos", "La respuesta requiere fecha UTC y estado APROBADO o RECHAZADO.", ErrorType.Validation));
        if (Estado == estadoRespuesta) return Result<Unit>.Success(Unit.Value);
        if (Estado != "PENDIENTE") return Error("pago.estado", "El intento ya tiene otro resultado.");
        Estado = estadoRespuesta;
        return Result<Unit>.Success(Unit.Value);
    }

    private static Result<Unit> Error(string codigo, string mensaje) =>
        Result<Unit>.Failure(new Error(codigo, mensaje, ErrorType.Conflict));
}
