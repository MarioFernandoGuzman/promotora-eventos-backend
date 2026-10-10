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
    CompraOptions options, IClock clock, IAuditLogger audit, IOutboxWriter outbox, ICompraProcesamientoEstado procesamiento) : ICommandHandler<CrearCompraCommand, CrearCompraResultado>
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
        var items = request.Items.GroupBy(x => x.TipoEntradaId).OrderBy(g => g.Key)
            .Select(g => new ItemCompra(g.Key, checked((int)g.Sum(x => (long)x.Cantidad)))).ToArray();
        var entradas = await compras.LockEntradasAsync(evento.Id, items.Select(x => x.TipoEntradaId).ToArray(), token);
        if (entradas.Count != items.Length) return Fallo("compra.entrada", "Hay entradas inactivas o que no pertenecen al evento.", ErrorType.BusinessRule);
        ahora = clock.UtcNow;
        if (ahora >= evento.FinVenta) return Fallo("compra.ventana", "La ventana de venta terminó durante la solicitud.", ErrorType.Conflict);
        var precios = entradas.ToDictionary(x => x.TipoEntradaId);
        var dominio = factory.Crear(Guid.NewGuid(), clienteId.Value, evento.Id, ahora, ahora.AddMinutes(options.ReservaMinutos), request.IdempotencyKey,
            options.Moneda, items.Select(x => new EntradaCompra(x.TipoEntradaId, precios[x.TipoEntradaId].EventoId, x.Cantidad, precios[x.TipoEntradaId].Precio)), evento.MaxBoletos);
        if (dominio.IsFailure) return Result<CrearCompraResultado>.Failure(dominio.Errors);
        foreach (var item in items)
        {
            if (!await cupos.ReservarAsync(evento.Id, item.TipoEntradaId, item.Cantidad, token))
                return Fallo("compra.agotado", "Cupo insuficiente; no se reservó ninguna entrada de esta solicitud.", ErrorType.Conflict);
        }
        var compra = dominio.Value;
        int id = await compras.InsertAsync(compra, token);
        await audit.WriteAsync(usuarioId, "CREATE", "compras", id.ToString(CultureInfo.InvariantCulture), actual.IpOrigen, token);
        await outbox.EscribirAsync(new("Compra", compra.PublicId.ToString("D"), TiposMensaje.ProcesarCompra,
            JsonSerializer.SerializeToElement(new { publicId = compra.PublicId })), token);
        return Result<CrearCompraResultado>.Success(new(ToDto(compra), false, procesamiento.Disponible));
    }, ct);

    private static Result<CrearCompraResultado> Fallo(string codigo, string mensaje, ErrorType tipo) => Result<CrearCompraResultado>.Failure(new Error(codigo, mensaje, tipo));
    private static CompraCreadaDto ToDto(Compra c) => new(c.PublicId, c.EventoId, c.Estado.ToString(), c.FechaCompra, c.ExpiraEn, c.Moneda,
        c.Importes.Subtotal, c.Importes.ComisionPlataforma, c.Importes.Impuestos, c.Importes.Total,
        c.Detalles.Select(d => new DetalleCompraDto(d.TipoEntradaId, d.Cantidad, d.PrecioUnitario, d.Subtotal)).ToArray());
}
