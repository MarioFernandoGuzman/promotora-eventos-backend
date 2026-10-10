using System.Globalization;
using Promotora.Application.Audit;
using Promotora.Application.Identity.Ports;
using Promotora.Application.Shared;
using Promotora.Application.Shared.Handlers;
using Promotora.Application.Shared.Ports;
using Promotora.Application.Ticketing.Ports;
using Promotora.Domain.Shared;
using Promotora.Domain.Ticketing;

namespace Promotora.Application.Ticketing;

public sealed class ObtenerCompraHandler(ICompraConsultaRepository compras, ClienteCompraAccess access) : IQueryHandler<ObtenerCompraQuery, CompraCreadaDto>
{
    public async Task<Result<CompraCreadaDto>> HandleAsync(ObtenerCompraQuery request, CancellationToken ct)
    {
        if (await access.ClienteAsync(false, ct) is not int cliente) return Result<CompraCreadaDto>.Failure(ClienteCompraAccess.Forbidden);
        var compra = await compras.ObtenerAsync(request.PublicId, cliente, ct);
        return compra is null ? Result<CompraCreadaDto>.Failure(ClienteCompraAccess.NotFound) : Result<CompraCreadaDto>.Success(compra.Compra);
    }
}
public sealed class MisComprasHandler(ICompraConsultaRepository compras, ClienteCompraAccess access) : IQueryHandler<MisComprasQuery, PaginaDto<CompraCreadaDto>>
{
    public async Task<Result<PaginaDto<CompraCreadaDto>>> HandleAsync(MisComprasQuery request, CancellationToken ct)
    {
        if (await access.ClienteAsync(false, ct) is not int cliente) return Result<PaginaDto<CompraCreadaDto>>.Failure(ClienteCompraAccess.Forbidden);
        return Result<PaginaDto<CompraCreadaDto>>.Success(await compras.MisComprasAsync(cliente, request.Page, request.PageSize, ct));
    }
}
public sealed class MisBoletosHandler(ICompraConsultaRepository compras, ClienteCompraAccess access) : IQueryHandler<MisBoletosQuery, PaginaDto<BoletoClienteDto>>
{
    public async Task<Result<PaginaDto<BoletoClienteDto>>> HandleAsync(MisBoletosQuery request, CancellationToken ct)
    {
        if (await access.ClienteAsync(false, ct) is not int cliente) return Result<PaginaDto<BoletoClienteDto>>.Failure(ClienteCompraAccess.Forbidden);
        return Result<PaginaDto<BoletoClienteDto>>.Success(await compras.MisBoletosAsync(cliente, request.Page, request.PageSize, ct));
    }
}
public sealed class CancelarCompraHandler(ICompraConsultaRepository compras, ClienteCompraAccess access, ICupoEntradaRepository cupos,
    ITicketingTransaction transaction, IClock clock, IAuditLogger audit, IUsuarioActual actual) : ICommandHandler<CancelarCompraCommand, CompraCreadaDto>
{
    public Task<Result<CompraCreadaDto>> HandleAsync(CancelarCompraCommand request, CancellationToken ct) => transaction.ExecuteAsync(async token =>
    {
        if (await access.ClienteAsync(true, token) is not int cliente) return Result<CompraCreadaDto>.Failure(ClienteCompraAccess.Forbidden);
        var propia = await compras.ObtenerAsync(request.PublicId, cliente, token);
        if (propia is null) return Result<CompraCreadaDto>.Failure(ClienteCompraAccess.NotFound);
        if (propia.Compra.Estado == "CANCELADA") return Result<CompraCreadaDto>.Success(propia.Compra);
        if (propia.Compra.Estado != "PENDIENTE") return Conflicto();
        bool gano = await cupos.FinalizarYDevolverAsync(propia.Id, EstadoCompra.PENDIENTE, EstadoCompra.CANCELADA, clock.UtcNow, token);
        var resultado = await compras.ObtenerAsync(request.PublicId, cliente, token);
        if (!gano) return resultado?.Compra.Estado == "CANCELADA" ? Result<CompraCreadaDto>.Success(resultado.Compra) : Conflicto();
        await audit.WriteAsync(actual.UsuarioId, "UPDATE", "compras", propia.Id.ToString(CultureInfo.InvariantCulture), actual.IpOrigen, token);
        return Result<CompraCreadaDto>.Success(resultado!.Compra);
    }, ct);

    private static Result<CompraCreadaDto> Conflicto() => Result<CompraCreadaDto>.Failure(new Error("compra.estado", "Solo se puede cancelar una compra PENDIENTE.", ErrorType.Conflict));
}
