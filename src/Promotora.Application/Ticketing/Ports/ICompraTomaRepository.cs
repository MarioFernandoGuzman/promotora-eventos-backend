namespace Promotora.Application.Ticketing.Ports;

public sealed record CompraTomadaDto(int Id, Guid PublicId, decimal Subtotal, decimal ComisionPlataforma,
    decimal Impuestos, decimal Total, string Moneda);

public interface ICompraTomaRepository
{
    // Requiere transacción explícita; null = otro proceso ganó o el estado no es PENDIENTE.
    Task<CompraTomadaDto?> TomarAsync(Guid publicId, CancellationToken ct);
}
