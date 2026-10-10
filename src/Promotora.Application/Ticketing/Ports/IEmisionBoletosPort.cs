using Promotora.Domain.Finance;
using Promotora.Domain.Shared;
using Promotora.Domain.Ticketing;

namespace Promotora.Application.Ticketing.Ports;

public sealed record AsistentesDetalle(int DetalleCompraId, IReadOnlyList<DatosAsistente> Asistentes);
public sealed record DetalleParaEmision(int Id, int Cantidad);
public sealed record CompraParaEmision(int Id, Guid PublicId, string Estado, string Moneda, bool? EsCarrera, ImportesCompra Importes, IReadOnlyList<DetalleParaEmision> Detalles);
public sealed record EmisionBoletosOptions(int? CategoriaCarreraId = null);
public sealed record BoletoEmitidoDto(long Id, Guid PublicId, int DetalleCompraId, string CodigoQr, string Estado, DatosAsistente Asistente, DateTime FechaEmision);

// Servicio interno de finalización; no es un endpoint ni reemplaza la autorización de consultas.
public interface IEmisionBoletosPort
{
    Task<Result<IReadOnlyList<BoletoEmitidoDto>>> EmitirAsync(Guid compraPublicId, IReadOnlyList<AsistentesDetalle> asistentes, CancellationToken ct);
    Task<Result<IReadOnlyList<BoletoEmitidoDto>>> EnTransaccionAsync(Guid compraPublicId, IReadOnlyList<AsistentesDetalle> asistentes, CancellationToken ct);
}

public interface ITicketTokenService
{
    Result<string> Firmar(Guid boletoPublicId);
    bool Validar(string? token, out Guid boletoPublicId);
}

public interface IEmisionBoletosRepository
{
    Task<CompraParaEmision?> LockCompraAsync(Guid publicId, CancellationToken ct);
    Task<RespaldoCobro> RespaldoAsync(CompraParaEmision compra, CancellationToken ct);
    Task<IReadOnlyList<BoletoEmitidoDto>> ListarAsync(int compraId, CancellationToken ct);
    Task<IReadOnlyList<BoletoEmitidoDto>> InsertarAsync(IReadOnlyList<Boleto> boletos, CancellationToken ct);
}
