using System.Globalization;
using Promotora.Application.Audit;
using Promotora.Application.Shared.Ports;
using Promotora.Application.Ticketing.Ports;
using Promotora.Domain.Shared;
using Promotora.Domain.Ticketing;

namespace Promotora.Application.Ticketing;

public sealed class EmisionBoletosService(IEmisionBoletosRepository repository, IUnitOfWork unitOfWork, ITicketTokenService tokens,
    IClock clock, IAuditLogger audit) : IEmisionBoletosPort
{
    // El finalizador futuro puede invocar EnTransaccionAsync para confirmar ledger/estado/emisión juntos.
    public Task<Result<IReadOnlyList<BoletoEmitidoDto>>> EmitirAsync(Guid compraPublicId, IReadOnlyList<AsistentesDetalle> asistentes, CancellationToken ct)
        => unitOfWork.ExecuteInTransactionAsync(token => EnTransaccionAsync(compraPublicId, asistentes, token), ct);

    public async Task<Result<IReadOnlyList<BoletoEmitidoDto>>> EnTransaccionAsync(Guid compraPublicId, IReadOnlyList<AsistentesDetalle> asistentes, CancellationToken ct)
    {
        if (compraPublicId == Guid.Empty || asistentes is null) return Fallo("boleto.solicitud", "Solicitud de emisión inválida.", ErrorType.Validation);
        var compra = await repository.LockCompraAsync(compraPublicId, ct);
        if (compra is null) return Fallo("boleto.compra", "Compra no encontrada.", ErrorType.NotFound);
        if (compra.Estado != "COMPLETADA") return Fallo("boleto.estado", "Solo se emiten boletos de compras completadas.", ErrorType.Conflict);
        if (compra.EsCarrera is null) return Fallo("boleto.categoria", "Falta configurar un ID estable válido de la categoría de carreras.", ErrorType.BusinessRule);
        if (compra.Detalles.Count == 0 || compra.Detalles.Any(d => d.Cantidad <= 0)) return Fallo("boleto.detalles", "La compra no tiene detalles válidos.", ErrorType.BusinessRule);
        var existentes = await repository.ListarAsync(compra.Id, ct);
        if (existentes.Count > 0)
        {
            bool completo = compra.Detalles.All(d => existentes.Count(b => b.DetalleCompraId == d.Id) == d.Cantidad)
                && existentes.Count == compra.Detalles.Sum(d => (long)d.Cantidad);
            return completo ? Result<IReadOnlyList<BoletoEmitidoDto>>.Success(existentes)
                : Fallo("boleto.parcial", "La emisión existente está incompleta; requiere conciliación.", ErrorType.Conflict);
        }
        var respaldo = await repository.RespaldoAsync(compra, ct);
        if (!respaldo.PermiteEmision(compra.Importes)) return Fallo("boleto.finanzas", "La compra no tiene respaldo financiero consistente.", ErrorType.BusinessRule);
        if (asistentes.Any(a => a is null || a.Asistentes is null) || asistentes.Select(a => a.DetalleCompraId).Distinct().Count() != asistentes.Count
            || asistentes.Any(a => !compra.Detalles.Any(d => d.Id == a.DetalleCompraId)))
            return Fallo("boleto.asistentes", "Los asistentes no corresponden a los detalles de la compra.", ErrorType.Validation);
        var datos = asistentes.ToDictionary(a => a.DetalleCompraId, a => a.Asistentes);
        var boletos = new List<Boleto>();
        var ahora = clock.UtcNow;
        foreach (var detalle in compra.Detalles)
        {
            datos.TryGetValue(detalle.Id, out var personas);
            if (personas is not null && (personas.Count != detalle.Cantidad || personas.Any(p => p is null)) || compra.EsCarrera.Value && personas is null)
                return Fallo("boleto.asistentes", "Debe informarse un asistente por boleto; obligatorio en carreras.", ErrorType.BusinessRule);
            for (int i = 0; i < detalle.Cantidad; i++)
            {
                var publicId = Guid.NewGuid();
                var firmado = tokens.Firmar(publicId);
                if (firmado.IsFailure) return Result<IReadOnlyList<BoletoEmitidoDto>>.Failure(firmado.Errors);
                var boleto = Boleto.Crear(publicId, detalle.Id, firmado.Value, personas?[i], compra.EsCarrera.Value, ahora);
                if (boleto.IsFailure) return Result<IReadOnlyList<BoletoEmitidoDto>>.Failure(boleto.Errors);
                boletos.Add(boleto.Value);
            }
        }
        var emitidos = await repository.InsertarAsync(boletos, ct);
        foreach (var boleto in emitidos)
            await audit.WriteAsync(null, "CREATE", "boletos", boleto.Id.ToString(CultureInfo.InvariantCulture), null, ct);
        return Result<IReadOnlyList<BoletoEmitidoDto>>.Success(emitidos);
    }

    private static Result<IReadOnlyList<BoletoEmitidoDto>> Fallo(string codigo, string mensaje, ErrorType tipo) => Result<IReadOnlyList<BoletoEmitidoDto>>.Failure(new Error(codigo, mensaje, tipo));
}
