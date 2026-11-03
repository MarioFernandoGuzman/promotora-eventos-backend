using Promotora.Domain.Identity;
using Promotora.Domain.Shared;

namespace Promotora.Application.Identity.Ports;

public interface ISesionRepository
{
    // Confirma también rechazos esperados: una reutilización debe persistir revocación/auditoría.
    // Las excepciones revierten la operación. No admite transacciones anidadas.
    Task<Result<T>> ExecuteAtomicAsync<T>(Func<CancellationToken, Task<Result<T>>> operation, CancellationToken ct);
    Task LockUsuarioAsync(int usuarioId, CancellationToken ct);
    Task<RefreshToken?> FindAsync(string tokenHash, CancellationToken ct);
    Task<long> AddAsync(int usuarioId, string hash, Guid familiaId, DateTime expiraEn, string? ip, CancellationToken ct);
    Task RevokeAsync(long id, long? replacementId, CancellationToken ct);
    Task RevokeFamilyAsync(Guid familiaId, CancellationToken ct);
    Task RevokeUserAsync(int usuarioId, CancellationToken ct);
    Task<bool> IsActiveAsync(long id, int usuarioId, CancellationToken ct);
}
