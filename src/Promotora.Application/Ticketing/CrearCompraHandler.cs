using System.Globalization;
using System.Text.Json;
using Promotora.Application.Audit;
using Promotora.Application.Identity.Ports;
using Promotora.Application.Shared.Handlers;
using Promotora.Application.Shared.Messaging;
using Promotora.Application.Shared.Ports;
using Promotora.Application.Ticketing.Ports;
using Promotora.Domain.Identity;
using Promotora.Domain.Shared;
using Promotora.Domain.Ticketing;

namespace Promotora.Application.Ticketing;

public sealed class CrearCompraHandler(ICompraRepository compras, ICupoEntradaRepository cupos, ITicketingTransaction transaction,
    IUsuarioRepository usuarios, ISesionRepository sesiones, IUsuarioActual actual, CrearCompraFactory factory,
    CompraOptions options, IClock clock, IAuditLogger audit, IOutboxWriter outbox, ICompraProcesamientoEstado procesamiento,
    EmisionBoletosOptions emisionOptions, IReservaAsientosPort? asientos = null) : ICommandHandler<CrearCompraCommand, CrearCompraResultado>
{
    public Task<Result<CrearCompraResultado>> HandleAsync(CrearCompraCommand request, CancellationToken ct) => transaction.ExecuteAsync(async token =>
    {
        if (actual.UsuarioId is not int usuarioId) return Fallo("compra.prohibida", "Se requiere un cliente activo.", ErrorType.Forbidden);
        await sesiones.LockUsuarioAsync(usuarioId, token);
        var actor = await usuarios.FindByIdAsync(usuarioId, token);
        if (actor is null || actor.Rol != RolUsuario.CLIENTE || !actor.PuedeAutenticarse || actor.DebeCambiarPassword)
            return Fallo("compra.prohibida", "Se requiere un cliente activo.", ErrorType.Forbidden);
        var clienteId = await compras.ClienteIdAsync(usuarioId, token);
        if (clienteId is null) return Fallo("compra.perfil", "El cliente no tiene perfil válido.", ErrorType.Forbidden);
        var existente = await compras.FindByKeyAsync(request.IdempotencyKey, token);
        if (existente is not null)
        {
            return existente.ClienteId == clienteId
                ? Result<CrearCompraResultado>.Success(new(existente.Compra, true, procesamiento.Disponible))
                : Fallo("compra.clave", "La clave de idempotencia ya está en uso.", ErrorType.Conflict);
        }

        var evento = await compras.LockEventoAsync(request.EventoId, token);
        if (evento is null) return Fallo("compra.evento", "Evento no encontrado.", ErrorType.NotFound);
        var ahora = clock.UtcNow;
        if (!evento.Activo || evento.Estado != "EN_VENTA" || evento.InicioVenta is null || evento.FinVenta is null
            || ahora < evento.InicioVenta || ahora >= evento.FinVenta)
            return Fallo("compra.ventana", "El evento no admite compras en este momento.", ErrorType.Conflict);
        long cantidad = request.Items.Sum(x => (long)x.Cantidad);
        if (cantidad > evento.MaxBoletos) return Fallo("compra.limite", "Se supera el máximo de boletos por compra.", ErrorType.BusinessRule);
        if (emisionOptions.CategoriaCarreraId is null)
            return Fallo("compra.configuracion", "Falta configurar el ID de la categoría de carreras.", ErrorType.BusinessRule);
        bool esCarrera = evento.CategoriaId == emisionOptions.CategoriaCarreraId;
        foreach (var item in request.Items)
        {
            if (item.Asistentes is not null && item.Asistentes.Count != item.Cantidad || esCarrera && item.Asistentes is null)
                return Fallo("compra.asistentes", "Debe informarse un asistente por boleto; obligatorio en carreras.", ErrorType.BusinessRule);
            foreach (var asistente in item.Asistentes ?? [])
            {
                if (asistente is null) return Fallo("compra.asistentes", "Asistente inválido.", ErrorType.Validation);
                var valido = asistente.Validar(esCarrera);
                if (valido.IsFailure) return Result<CrearCompraResultado>.Failure(valido.Errors);
            }
        }
        var items = request.Items.GroupBy(x => x.TipoEntradaId).OrderBy(g => g.Key)
            .Select(g => new ItemCompra(g.Key, checked((int)g.Sum(x => (long)x.Cantidad)),
                g.Any(x => x.Asistentes is not null)
                    ? g.SelectMany(x => x.Asistentes ?? Enumerable.Repeat(new DatosAsistente(null, null), x.Cantidad)).ToArray() : null,
                g.Any(x => x.AsientoIds is not null) ? g.SelectMany(x => x.AsientoIds ?? []).ToArray() : null)).ToArray();
        var entradas = await compras.LockEntradasAsync(evento.Id, items.Select(x => x.TipoEntradaId).ToArray(), token);
        if (entradas.Count != items.Length) return Fallo("compra.entrada", "Hay entradas inactivas o que no pertenecen al evento.", ErrorType.BusinessRule);
        ahora = clock.UtcNow;
        if (ahora >= evento.FinVenta) return Fallo("compra.ventana", "La ventana de venta terminó durante la solicitud.", ErrorType.Conflict);
        var precios = entradas.ToDictionary(x => x.TipoEntradaId);
        var dominio = factory.Crear(Guid.NewGuid(), clienteId.Value, evento.Id, ahora, ahora.AddMinutes(options.ReservaMinutos), request.IdempotencyKey,
            options.Moneda, items.Select(x => new EntradaCompra(x.TipoEntradaId, precios[x.TipoEntradaId].EventoId, x.Cantidad, precios[x.TipoEntradaId].Precio)), evento.MaxBoletos);
        if (dominio.IsFailure) return Result<CrearCompraResultado>.Failure(dominio.Errors);
        if (asientos is not null)
        {
            var reserva = await asientos.ReservarAsync(evento.Id, items, token);
            if (reserva.IsFailure) return Result<CrearCompraResultado>.Failure(reserva.Errors);
        }
        else if (items.Any(x => x.AsientoIds is not null))
            return Fallo("numeracion.configuracion", "Reserva numerada no disponible.", ErrorType.BusinessRule);
        foreach (var item in items)
        {
            if (!await cupos.ReservarAsync(evento.Id, item.TipoEntradaId, item.Cantidad, token))
                return Fallo("compra.agotado", "Cupo insuficiente; no se reservó ninguna entrada de esta solicitud.", ErrorType.Conflict);
        }
        var compra = dominio.Value;
        int id = await compras.InsertAsync(compra, token);
        if (asientos is not null) await asientos.AsignarAsync(id, items, ahora, token);
        await audit.WriteAsync(usuarioId, "CREATE", "compras", id.ToString(CultureInfo.InvariantCulture), actual.IpOrigen, token);
        await outbox.EscribirAsync(new("Compra", compra.PublicId.ToString("D"), TiposMensaje.ProcesarCompra,
            JsonSerializer.SerializeToElement(new
            {
                publicId = compra.PublicId,
                asistentes = items.Where(x => x.Asistentes is not null).Select(x => new { tipoEntradaId = x.TipoEntradaId, personas = x.Asistentes })
            })), token);
        var dto = ToDto(compra);
        dto = dto with { Items = dto.Items.Select(d => d with { AsientoIds = items.Single(i => i.TipoEntradaId == d.TipoEntradaId).AsientoIds }).ToArray() };
        return Result<CrearCompraResultado>.Success(new(dto, false, procesamiento.Disponible));
    }, ct);

    private static Result<CrearCompraResultado> Fallo(string codigo, string mensaje, ErrorType tipo) => Result<CrearCompraResultado>.Failure(new Error(codigo, mensaje, tipo));
    private static CompraCreadaDto ToDto(Compra c) => new(c.PublicId, c.EventoId, c.Estado.ToString(), c.FechaCompra, c.ExpiraEn, c.Moneda,
        c.Importes.Subtotal, c.Importes.ComisionPlataforma, c.Importes.Impuestos, c.Importes.Total,
        c.Detalles.Select(d => new DetalleCompraDto(d.TipoEntradaId, d.Cantidad, d.PrecioUnitario, d.Subtotal)).ToArray());
}
