using Promotora.Domain.Shared;

namespace Promotora.Domain.Reviews;

public static class EstadoResena
{
    public static bool Existe(string estado) => estado is "PENDIENTE" or "APROBADA" or "RECHAZADA";
    public static Result<string> Editar(string actual, string esperado, bool mismoContenido) => Propia(actual, esperado, "PENDIENTE", actual == "PENDIENTE" && mismoContenido);
    public static Result<string> Retirar(string actual, string esperado) => Propia(actual, esperado, "RECHAZADA", actual == "RECHAZADA");
    private static Result<string> Propia(string actual, string esperado, string destino, bool repetida)
    {
        if (!Existe(actual) || !Existe(esperado)) return Result<string>.Failure(new Error("resena.estado", "Estado inválido.", ErrorType.Validation));
        if (repetida || actual == esperado) return Result<string>.Success(destino);
        return Result<string>.Failure(new Error("resena.conflicto", "La reseña cambió de estado; consultar antes de reintentar.", ErrorType.Conflict));
    }
    public static Result<string> Moderar(string actual, string nuevo)
    {
        if (nuevo is not ("APROBADA" or "RECHAZADA"))
            return Result<string>.Failure(new Error("resena.estado", "La moderación solo aprueba o rechaza.", ErrorType.Validation));
        if (actual == nuevo || actual == "PENDIENTE") return Result<string>.Success(nuevo);
        return Result<string>.Failure(new Error("resena.conflicto", "Solo se modera una reseña PENDIENTE.", ErrorType.Conflict));
    }
}
