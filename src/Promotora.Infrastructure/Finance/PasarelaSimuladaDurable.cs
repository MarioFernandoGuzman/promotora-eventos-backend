using Npgsql;
using Promotora.Application.Finance.Ports;
using Promotora.Application.Shared.Ports;
using Promotora.Domain.Shared;

namespace Promotora.Infrastructure.Finance;

// El diario es el efecto del proveedor simulado; su conexión nunca comparte la transacción de compra.
public sealed class PasarelaSimuladaDurable(string connectionString, FakePaymentOptions options, IClock clock) : IPaymentGateway
{
    public async Task<Result<RespuestaPago>> CobrarAsync(SolicitudPago solicitud, CancellationToken ct)
    {
        var simulador = new FakePaymentGateway(options, clock);
        var propuesta = await simulador.CobrarAsync(solicitud, ct);
        if (propuesta.IsFailure) return propuesta;
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand("INSERT INTO public.pagos_simulados (referencia,monto,moneda,estado,fecha) VALUES (@referencia,@monto,@moneda,@estado,@fecha) ON CONFLICT (referencia) DO NOTHING", connection);
        command.Parameters.AddWithValue("referencia", solicitud.Referencia);
        command.Parameters.AddWithValue("monto", solicitud.Monto);
        command.Parameters.AddWithValue("moneda", solicitud.Moneda);
        command.Parameters.AddWithValue("estado", propuesta.Value.Estado);
        command.Parameters.AddWithValue("fecha", propuesta.Value.Fecha);
        await command.ExecuteNonQueryAsync(ct); // Autocommit antes de devolver el resultado.
        var persistido = (await LeerAsync(connection, solicitud.Referencia, ct))!;
        if (persistido.Monto != solicitud.Monto || persistido.Moneda != solicitud.Moneda)
            return Result<RespuestaPago>.Failure(new Error("pago.referencia", "La referencia ya tiene otros importes.", ErrorType.Conflict));
        return Result<RespuestaPago>.Success(persistido);
    }

    public async Task<RespuestaPago?> ConsultarAsync(Guid referencia, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        return await LeerAsync(connection, referencia, ct);
    }

    private static async Task<RespuestaPago?> LeerAsync(NpgsqlConnection connection, Guid referencia, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("SELECT monto,moneda,estado,fecha FROM public.pagos_simulados WHERE referencia=@referencia", connection);
        command.Parameters.AddWithValue("referencia", referencia);
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? new(referencia, reader.GetDecimal(0), reader.GetString(1), "SIMULADA", reader.GetString(2), reader.GetDateTime(3)) : null;
    }
}
