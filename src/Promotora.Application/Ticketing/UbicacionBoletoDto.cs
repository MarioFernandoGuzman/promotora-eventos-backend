namespace Promotora.Application.Ticketing;

public sealed record UbicacionBoletoDto(long AsientoId, string Silla, string? Fila, long? MesaId, string? Mesa, int LocalidadId);
