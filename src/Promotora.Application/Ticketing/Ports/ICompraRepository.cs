using Promotora.Domain.Shared;
using Promotora.Domain.Ticketing;

namespace Promotora.Application.Ticketing.Ports;

public sealed record CompraExistente(int ClienteId, CompraCreadaDto Compra);
public sealed record EventoCompra(int Id, string Estado, bool Activo, DateTime? InicioVenta, DateTime? FinVenta, int MaxBoletos, int CategoriaId = 0);
public sealed record EntradaDisponible(int TipoEntradaId, int EventoId, decimal Precio);

public interface ICompraRepository
{
    Task<int?> ClienteIdAsync(int usuarioId, CancellationToken ct);
    Task<CompraExistente?> FindByKeyAsync(string key, CancellationToken ct);
    Task<EventoCompra?> LockEventoAsync(int eventoId, CancellationToken ct);
    Task<IReadOnlyList<EntradaDisponible>> LockEntradasAsync(int eventoId, IReadOnlyList<int> tipos, CancellationToken ct);
    Task<int> InsertAsync(Compra compra, CancellationToken ct);
}

public interface ITicketingTransaction
{
    Task<Result<T>> ExecuteAsync<T>(Func<CancellationToken, Task<Result<T>>> operation, CancellationToken ct);
}
