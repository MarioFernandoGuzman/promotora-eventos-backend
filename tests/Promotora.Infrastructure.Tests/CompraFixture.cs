using Microsoft.EntityFrameworkCore;
using Promotora.Infrastructure.Catalog.Persistence;
using Promotora.Infrastructure.Identity.Persistence;
using Promotora.Infrastructure.Shared.Persistence;
using Promotora.Infrastructure.Venues.Persistence;

namespace Promotora.Infrastructure.Tests;

public static class CompraFixture
{
    public static async Task<(int EventoId, int TipoId, int ClienteId)> CrearAsync(PromotoraDbContext db, int cupo, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var dueno = new UsuarioRow { Email = $"{Guid.NewGuid():N}@example.invalid", PasswordHash = "!SIN_ACCESO_PRUEBA!", Nombre = "Dueño", Apellido = "Prueba", Rol = "DUENO_RECINTO" };
        var cliente = new UsuarioRow { Email = $"{Guid.NewGuid():N}@example.invalid", PasswordHash = "!SIN_ACCESO_PRUEBA!", Nombre = "Cliente", Apellido = "Prueba", Rol = "CLIENTE" };
        db.Usuarios.AddRange(dueno, cliente);
        await db.SaveChangesAsync(ct);
        var perfilDueno = new DuenoRecintoRow { UsuarioId = dueno.Id, Empresa = "Pruebas" };
        var perfilCliente = new ClienteRow { UsuarioId = cliente.Id };
        db.DuenosRecinto.Add(perfilDueno); db.Clientes.Add(perfilCliente);
        await db.SaveChangesAsync(ct);
        var recinto = new RecintoRow { DuenoId = perfilDueno.Id, Nombre = "Prueba" };
        db.Recintos.Add(recinto);
        await db.SaveChangesAsync(ct);
        var evento = new EventoRow { RecintoId = recinto.Id, CategoriaId = await db.CategoriasEvento.Select(c => c.Id).FirstAsync(ct), CreadoPor = dueno.Id, Nombre = "Venta", Estado = "EN_VENTA", FechaInicio = DateTime.UtcNow.AddDays(2), FechaInicioVenta = DateTime.UtcNow.AddDays(-1), FechaFinVenta = DateTime.UtcNow.AddDays(1) };
        db.Eventos.Add(evento);
        await db.SaveChangesAsync(ct);
        var tipo = new TipoEntradaRow { EventoId = evento.Id, Nombre = "General", Precio = 100m, CupoTotal = cupo, CupoDisponible = cupo };
        db.TiposEntrada.Add(tipo);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return (evento.Id, tipo.Id, perfilCliente.Id);
    }
}
