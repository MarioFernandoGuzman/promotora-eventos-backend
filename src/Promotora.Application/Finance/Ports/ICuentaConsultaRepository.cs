using Promotora.Application.Shared;

namespace Promotora.Application.Finance.Ports;

public interface ICuentaConsultaRepository
{
    Task<PaginaDto<CuentaDto>> ListarAsync(CuentasQuery query, int? duenoPermitido, CancellationToken ct);
    Task<CuentaDto?> ObtenerAsync(int id, int? duenoPermitido, CancellationToken ct);
    Task<PaginaDto<MovimientoCuentaDto>> MovimientosAsync(MovimientosCuentaQuery query, CancellationToken ct);
}
