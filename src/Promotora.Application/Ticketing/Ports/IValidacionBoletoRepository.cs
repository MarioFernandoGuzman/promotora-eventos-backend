namespace Promotora.Application.Ticketing.Ports;

public sealed record BoletoParaValidar(long Id, Guid PublicId, int DuenoId, string Estado, string EstadoCompra);
public interface IValidacionBoletoRepository
{
    Task<BoletoParaValidar?> LockAsync(Guid publicId, string codigoQr, CancellationToken ct);
    Task<bool> UsarAsync(long id, string codigoQr, int usuarioId, DateTime fechaUso, CancellationToken ct);
}
