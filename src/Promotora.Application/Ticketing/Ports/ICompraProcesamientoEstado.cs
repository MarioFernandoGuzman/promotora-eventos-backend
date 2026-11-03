namespace Promotora.Application.Ticketing.Ports;

// Solo el módulo que integra consumidor + pago + ledger puede declarar disponible el flujo.
public interface ICompraProcesamientoEstado
{
    bool Disponible { get; }
}
