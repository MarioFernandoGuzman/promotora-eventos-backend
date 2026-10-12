using Promotora.Domain.Shared;

namespace Promotora.Domain.Reviews;

public static class EstadoResena
{
    public static bool Existe(string estado) => estado is "PENDIENTE" or "APROBADA" or "RECHAZADA";
    public static Result<string> Moderar(string actual, string nuevo)
    {
        if (nuevo is not ("APROBADA" or "RECHAZADA"))
            return Result<string>.Failure(new Error("resena.estado", "La moderación solo aprueba o rechaza.", ErrorType.Validation));
        if (actual == nuevo || actual == "PENDIENTE") return Result<string>.Success(nuevo);
        return Result<string>.Failure(new Error("resena.conflicto", "Solo se modera una reseña PENDIENTE.", ErrorType.Conflict));
    }
}
