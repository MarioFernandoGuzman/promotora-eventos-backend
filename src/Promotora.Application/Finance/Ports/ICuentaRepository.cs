using Promotora.Domain.Finance;

namespace Promotora.Application.Finance.Ports;

public interface IAltaCuentaDuenoPort
{
    Task CrearEnTransaccionAsync(int duenoId, int? actorId, string? ip, CancellationToken ct);
}

public interface ICuentaRepository
{
    Task<int?> DuenoIdAsync(int usuarioId, CancellationToken ct);
    Task<CuentaFinanciera?> LockAsync(int id, CancellationToken ct);
    Task<bool> CerrarAsync(int id, DateTime fecha, CancellationToken ct);
}
