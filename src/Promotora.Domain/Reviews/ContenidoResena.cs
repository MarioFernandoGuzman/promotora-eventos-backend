using Promotora.Domain.Shared;

namespace Promotora.Domain.Reviews;

public sealed record ContenidoResena(short Calificacion, string? Comentario)
{
    public static Result<ContenidoResena> Crear(short calificacion, string? comentario)
    {
        if (calificacion is < 1 or > 5 || comentario?.Length > 510)
            return Result<ContenidoResena>.Failure(new Error("resena.contenido", "Calificación entre 1 y 5 y comentario de máximo 510 caracteres.", ErrorType.Validation));
        return Result<ContenidoResena>.Success(new(calificacion, string.IsNullOrWhiteSpace(comentario) ? null : comentario.Trim()));
    }
}
