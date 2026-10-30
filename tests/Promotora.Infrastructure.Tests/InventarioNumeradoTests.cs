using Microsoft.EntityFrameworkCore;
using Npgsql;
using Promotora.Infrastructure.Ticketing.Persistence;
using Promotora.Infrastructure.Venues.Persistence;

namespace Promotora.Infrastructure.Tests;

public sealed class InventarioNumeradoTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task PublicacionCongelaEstructuraYRechazaCupoSinAsignacion()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.CreateContext();
        var datos = await CompraFixture.CrearAsync(db, 2, ct);
        int recinto = await db.Eventos.Where(e => e.Id == datos.EventoId).Select(e => e.RecintoId).SingleAsync(ct);
        await db.Recintos.Where(r => r.Id == recinto).ExecuteUpdateAsync(s => s.SetProperty(r => r.CapacidadTotal, 10), ct);
        var localidad = new LocalidadRow { RecintoId = recinto, Nombre = "Numerada", Capacidad = 2 };
        db.Localidades.Add(localidad); await db.SaveChangesAsync(ct);
        await db.TiposEntrada.Where(t => t.Id == datos.TipoId).ExecuteUpdateAsync(s => s.SetProperty(t => t.LocalidadId, localidad.Id), ct);
        var distribucion = new DistribucionEventoRow { EventoId = datos.EventoId, LocalidadId = localidad.Id };
        db.DistribucionesEvento.Add(distribucion); await db.SaveChangesAsync(ct);
        var mesa = new MesaEventoRow { DistribucionId = distribucion.Id, TipoEntradaId = datos.TipoId, Etiqueta = "M1", Modalidad = "COMPLETA" };
        db.MesasEvento.Add(mesa); await db.SaveChangesAsync(ct);
        db.AsientosEvento.AddRange(new AsientoEventoRow { DistribucionId = distribucion.Id, MesaId = mesa.Id, TipoEntradaId = datos.TipoId, Etiqueta = "M1-S1" },
            new AsientoEventoRow { DistribucionId = distribucion.Id, MesaId = mesa.Id, TipoEntradaId = datos.TipoId, Etiqueta = "M1-S2" });
        await db.SaveChangesAsync(ct);
        await db.DistribucionesEvento.Where(d => d.Id == distribucion.Id && d.Estado == "BORRADOR")
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.Estado, "PUBLICADA").SetProperty(d => d.FechaPublicacion, DateTime.UtcNow), ct);
        var error = await Assert.ThrowsAsync<PostgresException>(() => db.TiposEntrada.Where(t => t.Id == datos.TipoId)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.CupoDisponible, 1), ct));
        Assert.Equal("23514", error.SqlState);
        error = await Assert.ThrowsAsync<PostgresException>(() => db.MesasEvento.Where(m => m.Id == mesa.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.Etiqueta, "Otra"), ct));
        Assert.Equal("23514", error.SqlState);
        Assert.Equal(2, await db.AsientosEvento.CountAsync(a => a.DistribucionId == distribucion.Id && a.Estado == "DISPONIBLE", ct));
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            await db.AsientosEvento.Where(a => a.DistribucionId == distribucion.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.Estado, "RESERVADO"), ct);
            await db.TiposEntrada.Where(t => t.Id == datos.TipoId)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.CupoDisponible, 0), ct);
            error = await Assert.ThrowsAsync<PostgresException>(() => tx.CommitAsync(ct));
            Assert.Equal("23514", error.SqlState);
        }
        Assert.Equal(2, await db.AsientosEvento.CountAsync(a => a.DistribucionId == distribucion.Id && a.Estado == "DISPONIBLE", ct));
        await using var conn = new NpgsqlConnection(fixture.ConnectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT has_table_privilege(current_user,'asignaciones_asiento','DELETE')", conn);
        Assert.Equal(false, await cmd.ExecuteScalarAsync(ct));
    }
}
