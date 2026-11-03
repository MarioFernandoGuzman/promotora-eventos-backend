using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Promotora.Application.Finance.Ports;
using Promotora.Application.Shared.Ports;
using Promotora.Infrastructure.Finance;
using Promotora.Infrastructure.Identity.Persistence;
using Promotora.Infrastructure.Shared;
using Promotora.Infrastructure.Shared.Messaging;
using Promotora.Infrastructure.Shared.Persistence;
using Promotora.Infrastructure.Ticketing;
using Promotora.Infrastructure.Ticketing.Persistence;

namespace Promotora.Infrastructure.Tests;

public sealed record VentaReembolso(int Id, Guid PublicId, int DetalleId, int TipoId, decimal Total);

public static class ReembolsoFixture
{
    public static async Task<VentaReembolso> VentaAsync(PromotoraDbContext db, string connectionString, byte[] key, bool gratis, CancellationToken ct)
    {
        var clock = new SystemClock(TimeProvider.System); var datos = await CompraFixture.CrearAsync(db, 5, ct);
        decimal precio = gratis ? 0m : 100m;
        await db.TiposEntrada.Where(t => t.Id == datos.TipoId).ExecuteUpdateAsync(s => s.SetProperty(t => t.Precio, precio), ct);
        var services = new ServiceCollection(); services.AddPromotoraInfrastructure(connectionString);
        services.AddEmisionBoletos(key, 5); services.AddFinalizacionCompras();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope(); var sp = scope.ServiceProvider;
        var trabajo = sp.GetRequiredService<PromotoraDbContext>();
        var importes = Promotora.Domain.Ticketing.ImportesCompra.Calcular(precio * 3, new(.12m, .10m)).Value;
        var compra = new CompraRow
        {
            PublicId = Guid.NewGuid(),
            ClienteId = datos.ClienteId,
            EventoId = datos.EventoId,
            FechaCompra = clock.UtcNow,
            ExpiraEn = clock.UtcNow.AddMinutes(15),
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            Estado = "PENDIENTE",
            Moneda = "GTQ",
            Subtotal = importes.Subtotal,
            ComisionPlataforma = importes.ComisionPlataforma,
            Impuestos = importes.Impuestos,
            Total = importes.Total
        };
        long intentoId; int detalleId;
        await using (var tx = await trabajo.Database.BeginTransactionAsync(ct))
        {
            Assert.True(await new EfCupoEntradaRepository(trabajo).ReservarAsync(datos.EventoId, datos.TipoId, 3, ct));
            trabajo.Compras.Add(compra); await trabajo.SaveChangesAsync(ct);
            var detalle = new DetalleCompraRow { CompraId = compra.Id, TipoEntradaId = datos.TipoId, Cantidad = 3, PrecioUnitario = precio, Subtotal = importes.Subtotal };
            trabajo.DetalleCompra.Add(detalle); await trabajo.SaveChangesAsync(ct); detalleId = detalle.Id;
            await trabajo.Compras.Where(c => c.Id == compra.Id && c.Estado == "PENDIENTE").ExecuteUpdateAsync(s => s.SetProperty(c => c.Estado, "PROCESANDO").SetProperty(c => c.ProcesandoDesde, clock.UtcNow), ct);
            var intento = await sp.GetRequiredService<IRegistroPagoPort>().IniciarEnTransaccionAsync(compra.PublicId, "SIMULADA", ct);
            Assert.True(intento.IsSuccess); intentoId = intento.Value.Id;
            await tx.CommitAsync(ct); trabajo.ChangeTracker.Clear();
        }
        var respuesta = await new PasarelaSimuladaDurable(connectionString, new(), clock).CobrarAsync(new(compra.PublicId, compra.Total, compra.Moneda), ct);
        var completada = await sp.GetRequiredService<ICompletarCompraPort>().CompletarAsync(compra.PublicId, intentoId, respuesta.Value, [], ct);
        Assert.True(completada.IsSuccess); return new(compra.Id, compra.PublicId, detalleId, datos.TipoId, compra.Total);
    }

    public static async Task<int> AdminAsync(PromotoraDbContext db, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var usuario = new UsuarioRow { Email = $"{Guid.NewGuid():N}@example.invalid", PasswordHash = "!SIN_ACCESO_PRUEBA!", Nombre = "Admin", Apellido = "Prueba", Rol = "ADMINISTRADOR" };
        db.Usuarios.Add(usuario); await db.SaveChangesAsync(ct);
        db.Administradores.Add(new AdministradorRow { UsuarioId = usuario.Id }); await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct); return usuario.Id;
    }
}
