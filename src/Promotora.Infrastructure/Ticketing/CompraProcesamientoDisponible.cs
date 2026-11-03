using Promotora.Application.Ticketing.Ports;

namespace Promotora.Infrastructure.Ticketing;

// El módulo dispone del procesador transaccional; el Worker se ejecuta como host separado.
public sealed class CompraProcesamientoDisponible : ICompraProcesamientoEstado
{
    public bool Disponible => true;
}
