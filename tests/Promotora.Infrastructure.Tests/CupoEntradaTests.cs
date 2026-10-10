using Microsoft.EntityFrameworkCore;
using Promotora.Domain.Ticketing;
using Promotora.Infrastructure.Ticketing;
using Promotora.Infrastructure.Ticketing.Persistence;

namespace Promotora.Infrastructure.Tests;

public sealed class CupoEntradaTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task ReservasConcurrentesNoSobrevendenYRollbackRestauraCupo()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var seed = fixture.CreateContext();
        var datos = await CompraFixture.CrearAsync(seed, 5, ct);
        var resultados = await Task.WhenAll(Enumerable.Range(0, 20).Select(async _ =>
        {
            await using var db = fixture.CreateContext();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var reservado = await new EfCupoEntradaRepository(db).ReservarAsync(datos.EventoId, datos.TipoId, 1, ct);
            await tx.CommitAsync(ct);
            return reservado;
        }));
        Assert.Equal(5, resultados.Count(x => x));
        Assert.Equal(0, await seed.TiposEntrada.Where(t => t.Id == datos.TipoId).Select(t => t.CupoDisponible).SingleAsync(ct));
        var otro = await CompraFixture.CrearAsync(seed, 3, ct);
        await using (var db = fixture.CreateContext())
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            Assert.True(await new EfCupoEntradaRepository(db).ReservarAsync(otro.EventoId, otro.TipoId, 2, ct));
            await tx.RollbackAsync(ct);
        }
        Assert.Equal(3, await seed.TiposEntrada.Where(t => t.Id == otro.TipoId).Select(t => t.CupoDisponible).SingleAsync(ct));
    }

    [Fact]
    public async Task CancelarYExpirarEnCarreraDevuelvenExactamenteUnaVez()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var seed = fixture.CreateContext();
        var datos = await CompraFixture.CrearAsync(seed, 5, ct);
        var ahora = DateTime.UtcNow;
        var compra = new CompraRow { ClienteId = datos.ClienteId, EventoId = datos.EventoId, Estado = "PENDIENTE", IdempotencyKey = Guid.NewGuid().ToString(), Moneda = "GTQ", FechaCompra = ahora.AddMinutes(-10), ExpiraEn = ahora.AddMinutes(-1) };
        await using (var tx = await seed.Database.BeginTransactionAsync(ct))
        {
            Assert.True(await new EfCupoEntradaRepository(seed).ReservarAsync(datos.EventoId, datos.TipoId, 3, ct));
            seed.Compras.Add(compra);
            await seed.SaveChangesAsync(ct);
            // Dos detalles del mismo tipo deben devolver su suma.
            seed.DetalleCompra.AddRange(new DetalleCompraRow { CompraId = compra.Id, TipoEntradaId = datos.TipoId, Cantidad = 1 }, new DetalleCompraRow { CompraId = compra.Id, TipoEntradaId = datos.TipoId, Cantidad = 2 });
            await seed.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        var resultados = await Task.WhenAll(Enumerable.Range(0, 20).Select(async i =>
        {
            await using var db = fixture.CreateContext();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            bool gano = await new EfCupoEntradaRepository(db).FinalizarYDevolverAsync(compra.Id, EstadoCompra.PENDIENTE, i % 2 == 0 ? EstadoCompra.CANCELADA : EstadoCompra.EXPIRADA, ahora, ct);
            await tx.CommitAsync(ct);
            return gano;
        }));
        Assert.Single(resultados, x => x);
        Assert.Equal(5, await seed.TiposEntrada.Where(t => t.Id == datos.TipoId).Select(t => t.CupoDisponible).SingleAsync(ct));
    }

    [Fact]
    public async Task RechazaReservaSinTransaccionCantidadInvalidaYTipoDeOtroEvento()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.CreateContext();
        var datos = await CompraFixture.CrearAsync(db, 5, ct);
        var repository = new EfCupoEntradaRepository(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.ReservarAsync(datos.EventoId, datos.TipoId, 1, ct));
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repository.ReservarAsync(datos.EventoId, datos.TipoId, 0, ct));
        Assert.False(await repository.ReservarAsync(int.MaxValue, datos.TipoId, 1, ct));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.FinalizarYDevolverAsync(1, EstadoCompra.PROCESANDO, EstadoCompra.CANCELADA, DateTime.UtcNow, ct));
        await tx.RollbackAsync(ct);
    }

    [Theory]
    [InlineData("PENDIENTE", false)]
    [InlineData("PROCESANDO", true)]
    public async Task DevolucionRespetaEstadoYRollbackRevierteCompraYCupo(string estado, bool puedeFallar)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.CreateContext();
        var datos = await CompraFixture.CrearAsync(db, 5, ct);
        var ahora = DateTime.UtcNow;
        var compra = new CompraRow { ClienteId = datos.ClienteId, EventoId = datos.EventoId, Estado = estado, IdempotencyKey = Guid.NewGuid().ToString(), Moneda = "GTQ", FechaCompra = ahora.AddMinutes(-1), ExpiraEn = ahora.AddMinutes(5) };
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            Assert.True(await new EfCupoEntradaRepository(db).ReservarAsync(datos.EventoId, datos.TipoId, 1, ct));
            db.Compras.Add(compra);
            await db.SaveChangesAsync(ct);
            db.DetalleCompra.Add(new DetalleCompraRow { CompraId = compra.Id, TipoEntradaId = datos.TipoId, Cantidad = 1 });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            var repository = new EfCupoEntradaRepository(db);
            Assert.False(await repository.FinalizarYDevolverAsync(compra.Id, EstadoCompra.PENDIENTE, EstadoCompra.EXPIRADA, ahora, ct));
            Assert.Equal(puedeFallar, await repository.FinalizarYDevolverAsync(compra.Id, EstadoCompra.PROCESANDO, EstadoCompra.FALLIDA, ahora, ct));
            if (!puedeFallar)
            {
                Assert.True(await repository.FinalizarYDevolverAsync(compra.Id, EstadoCompra.PENDIENTE, EstadoCompra.CANCELADA, ahora, ct));
            }
            Assert.Equal(5, await db.TiposEntrada.Where(t => t.Id == datos.TipoId).Select(t => t.CupoDisponible).SingleAsync(ct));
            await tx.RollbackAsync(ct);
        }
        Assert.Equal(estado, await db.Compras.Where(c => c.Id == compra.Id).Select(c => c.Estado).SingleAsync(ct));
        Assert.Equal(4, await db.TiposEntrada.Where(t => t.Id == datos.TipoId).Select(t => t.CupoDisponible).SingleAsync(ct));
    }
}
