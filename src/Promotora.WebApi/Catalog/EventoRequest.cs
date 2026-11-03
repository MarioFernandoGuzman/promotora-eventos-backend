using Promotora.Domain.Catalog;

namespace Promotora.WebApi.Catalog;

public sealed record EventoRequest(int RecintoId, int CategoriaId, string Nombre, DateTime FechaInicio,
    string? Descripcion = null, DateTime? FechaFin = null, DateTime? FechaInicioVenta = null, DateTime? FechaFinVenta = null,
    int MaxBoletosPorCompra = 10, string? ImagenUrl = null)
{
    public DatosEvento ToDomain() => new(RecintoId, CategoriaId, Nombre, Descripcion, FechaInicio, FechaFin,
        FechaInicioVenta, FechaFinVenta, MaxBoletosPorCompra, ImagenUrl);
}

public sealed record EstadoEventoRequest(string EstadoEsperado, string EstadoNuevo);
