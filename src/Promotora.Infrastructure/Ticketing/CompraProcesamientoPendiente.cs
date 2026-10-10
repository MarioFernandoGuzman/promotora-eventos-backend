using Promotora.Application.Ticketing.Ports;

namespace Promotora.Infrastructure.Ticketing;

public sealed class CompraProcesamientoPendiente : ICompraProcesamientoEstado
{
    public bool Disponible => false;
}
