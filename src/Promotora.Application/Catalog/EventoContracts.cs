using Promotora.Application.Shared;
using Promotora.Application.Shared.Handlers;
using Promotora.Domain.Catalog;
using Promotora.Domain.Shared;

namespace Promotora.Application.Catalog;

public sealed record EventoDto(int Id, int CreadoPor, int RecintoId, int CategoriaId, string Nombre, string? Descripcion,
    DateTime FechaInicio, DateTime? FechaFin, DateTime? FechaInicioVenta, DateTime? FechaFinVenta, int MaxBoletosPorCompra,
    string Estado, string? ImagenUrl, DateTime FechaCreacion)
{
    public static EventoDto From(Evento evento) => new(evento.Id, evento.CreadoPor, evento.Datos.RecintoId, evento.Datos.CategoriaId,
        evento.Datos.Nombre, evento.Datos.Descripcion, evento.Datos.FechaInicio, evento.Datos.FechaFin, evento.Datos.FechaInicioVenta,
        evento.Datos.FechaFinVenta, evento.Datos.MaxBoletosPorCompra, evento.Estado.ToString(), evento.Datos.ImagenUrl, evento.FechaCreacion);
}

public sealed record CrearEventoCommand(DatosEvento Datos) : ICommand<EventoDto>;
public sealed record ActualizarEventoCommand(int Id, DatosEvento Datos) : ICommand<EventoDto>;
public sealed record CambiarEstadoEventoCommand(int Id, string EstadoEsperado, string EstadoNuevo) : ICommand<EventoDto>;
public sealed record CancelarEventoCommand(int Id) : ICommand<Unit>;
public sealed record ObtenerEventoQuery(int Id) : IQuery<EventoDto>;
public sealed record ListarEventosQuery(int Page = 1, int PageSize = 20) : IQuery<PaginaDto<EventoDto>>;
