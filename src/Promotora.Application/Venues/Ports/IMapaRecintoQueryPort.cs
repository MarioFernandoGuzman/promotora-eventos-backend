namespace Promotora.Application.Venues.Ports;

public interface IMapaRecintoQueryPort
{
    Task<MapaRecintoDto?> ReadAsync(int recintoId, int? eventoId, CancellationToken ct);
}
