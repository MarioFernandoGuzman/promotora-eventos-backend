using Promotora.Application.Finance;
using Promotora.Domain.Shared;
using Promotora.Domain.Ticketing;

namespace Promotora.Application.Ticketing;

// La política se entrega al agregado al crear la compra; no se vuelve a aplicar a compras existentes.
public sealed class CrearCompraFactory(IPoliticaTarifariaProvider politica)
{
    public Result<Compra> Crear(Guid publicId, int clienteId, int eventoId, DateTime ahora, DateTime expiraEn,
        string idempotencyKey, string moneda, IEnumerable<EntradaCompra> entradas, int maxBoletosPorCompra)
        => Compra.Crear(publicId, clienteId, eventoId, ahora, expiraEn, idempotencyKey, moneda, entradas, maxBoletosPorCompra, politica.Obtener());
}
