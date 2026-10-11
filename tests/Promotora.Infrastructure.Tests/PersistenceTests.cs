using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using Promotora.Infrastructure.AI.Persistence;
using Promotora.Infrastructure.Catalog.Persistence;
using Promotora.Infrastructure.Identity.Persistence;
using Promotora.Infrastructure.Shared.Persistence;

namespace Promotora.Infrastructure.Tests;

public sealed class PersistenceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task TodasLasTablasYVistasSeConsultanConSusColumnasReales()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.CreateContext();
        var entities = db.Model.GetEntityTypes().ToArray();
        Assert.Equal(27, entities.Count(e => e.GetViewName() is null));
        Assert.Equal(16, entities.Count(e => e.GetViewName() is not null));
        Assert.Equal(203, entities.Where(e => e.GetViewName() is null).Sum(e => e.GetProperties().Count()));
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(ct);
        foreach (var entity in entities)
        {
            var view = entity.GetViewName();
            var table = view ?? entity.GetTableName()!;
            var schema = view is null ? "public" : "bi";
            if (view is not null)
            {
                Assert.Null(entity.FindPrimaryKey());
            }

            var id = view is null ? StoreObjectIdentifier.Table(table, schema) : StoreObjectIdentifier.View(table, schema);
            await using var command = new NpgsqlCommand("""
                SELECT a.attname, format_type(a.atttypid,a.atttypmod), a.attnotnull
                FROM pg_attribute a JOIN pg_class c ON c.oid=a.attrelid JOIN pg_namespace n ON n.oid=c.relnamespace
                WHERE n.nspname=@schema AND c.relname=@table AND a.attnum>0 AND NOT a.attisdropped
                """, connection);
            command.Parameters.AddWithValue("schema", schema);
            command.Parameters.AddWithValue("table", table);
            await using (var reader = await command.ExecuteReaderAsync(ct))
            {
                var count = 0;
                while (await reader.ReadAsync(ct))
                {
                    var property = Assert.Single(entity.GetProperties(), p => p.GetColumnName(id) == reader.GetString(0));
                    Assert.Equal(reader.GetString(1), property.GetColumnType());
                    Assert.Equal(!reader.GetBoolean(2), property.IsNullable);
                    count++;
                }

                Assert.Equal(entity.GetProperties().Count(), count);
            }

            var method = typeof(PersistenceTests).GetMethod(nameof(QueryAsync), BindingFlags.NonPublic | BindingFlags.Static)!.MakeGenericMethod(entity.ClrType);
            await (Task)method.Invoke(null, [db, ct])!;
        }

        Assert.Equal(6, await db.CategoriasEvento.CountAsync(ct));
        Assert.Equal(2, await db.CuentasFinancieras.CountAsync(ct));
    }

    private static async Task QueryAsync<T>(PromotoraDbContext db, CancellationToken ct) where T : class =>
        await db.Set<T>().AsNoTracking().Take(1).ToListAsync(ct);

    [Fact]
    public async Task FalseExplicitoNoSeSustituyePorDefaultTrue()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.CreateContext();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var categoria = new CategoriaEventoRow { Nombre = Guid.NewGuid().ToString("N"), Activo = false };
        db.CategoriasEvento.Add(categoria);
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
        Assert.False((await db.CategoriasEvento.SingleAsync(c => c.Id == categoria.Id, ct)).Activo);
        await transaction.RollbackAsync(ct);
    }

    [Fact]
    public async Task VersionCeroNoSeSustituyePorDefaultUno()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.CreateContext();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var usuario = new UsuarioRow { Email = $"{Guid.NewGuid():N}@example.invalid", PasswordHash = "!SIN_ACCESO_PRUEBA!", Nombre = "Prueba", Apellido = "Default", Rol = "ADMINISTRADOR" };
        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync(ct);
        db.Administradores.Add(new AdministradorRow { UsuarioId = usuario.Id });
        await db.SaveChangesAsync(ct);
        var documento = new DocumentoRagRow { Titulo = "Versión explícita", SubidoPor = usuario.Id, Version = 0 };
        db.DocumentosRag.Add(documento);
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
        Assert.Equal(0, (await db.DocumentosRag.SingleAsync(d => d.Id == documento.Id, ct)).Version);
        await transaction.RollbackAsync(ct);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DefaultsBooleanosDeIdentidadYOutboxConservanValorExplicito(bool valor)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = new PromotoraDbContext(new DbContextOptionsBuilder<PromotoraDbContext>()
            .UsePromotoraPostgres(fixture.ConnectionString)
            .ConfigureWarnings(warnings => warnings.Throw(RelationalEventId.BoolWithDefaultWarning)).Options);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var usuario = new UsuarioRow
        {
            Email = $"{Guid.NewGuid():N}@example.invalid",
            PasswordHash = "!SIN_ACCESO_PRUEBA!",
            Nombre = "Prueba",
            Apellido = "Sentinel",
            Rol = "CLIENTE",
            DebeCambiarPassword = valor
        };
        using var payload = JsonDocument.Parse("{\"contenido\":\"prueba\"}");
        var outbox = new OutboxMensajeRow { Agregado = "Prueba", AgregadoId = "1", Tipo = "prueba", Payload = payload, Publicado = valor };
        db.Usuarios.Add(usuario); db.OutboxMensajes.Add(outbox);
        await db.SaveChangesAsync(ct);
        db.Clientes.Add(new ClienteRow { UsuarioId = usuario.Id });
        await db.SaveChangesAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SET CONSTRAINTS ALL IMMEDIATE", ct);
        db.ChangeTracker.Clear();
        Assert.Equal(valor, (await db.Usuarios.SingleAsync(x => x.Id == usuario.Id, ct)).DebeCambiarPassword);
        Assert.Equal(valor, (await db.OutboxMensajes.SingleAsync(x => x.Id == outbox.Id, ct)).Publicado);
        await transaction.RollbackAsync(ct);
    }

    [Fact]
    public async Task IdentidadGeneraIdYDefaultsYExigePerfilAlCommit()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.CreateContext();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var usuario = new UsuarioRow { Email = $"{Guid.NewGuid():N}@example.invalid", PasswordHash = "!SIN_ACCESO_PRUEBA!", Nombre = "Prueba", Apellido = "Perfil", Rol = "CLIENTE" };
        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync(ct);
        Assert.True(usuario.Id > 0);
        Assert.Equal("ACTIVO", usuario.Estado);
        Assert.Equal(DateTimeKind.Utc, usuario.FechaRegistro.Kind);
        Assert.False(usuario.DebeCambiarPassword);
        db.Clientes.Add(new ClienteRow { UsuarioId = usuario.Id });
        await db.SaveChangesAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SET CONSTRAINTS ALL IMMEDIATE", ct);
        await transaction.RollbackAsync(ct);

        await using var invalidDb = fixture.CreateContext();
        invalidDb.Usuarios.Add(new UsuarioRow { Email = $"{Guid.NewGuid():N}@example.invalid", PasswordHash = "!SIN_ACCESO_PRUEBA!", Nombre = "Sin", Apellido = "Perfil", Rol = "CLIENTE" });
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => invalidDb.SaveChangesAsync(ct));
        Assert.Equal(PostgresErrorCodes.CheckViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
    }

    [Fact]
    public async Task Vector1536SeGuardaYSeRecuperaPorCoseno()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.CreateContext();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var usuario = new UsuarioRow { Email = $"{Guid.NewGuid():N}@example.invalid", PasswordHash = "!SIN_ACCESO_PRUEBA!", Nombre = "Prueba", Apellido = "Vector", Rol = "ADMINISTRADOR" };
        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync(ct);
        db.Administradores.Add(new AdministradorRow { UsuarioId = usuario.Id });
        var documento = new DocumentoRagRow { Titulo = "Prueba", SubidoPor = usuario.Id };
        db.DocumentosRag.Add(documento);
        await db.SaveChangesAsync(ct);
        var values = new float[1536];
        values[0] = 1;
        var vector = new Vector(values);
        db.ChunksRag.Add(new ChunkRagRow { DocumentoId = documento.Id, Orden = 0, Contenido = "Contenido público", Embedding = vector });
        await db.SaveChangesAsync(ct);
        var distance = await db.ChunksRag.Where(c => c.DocumentoId == documento.Id).Select(c => c.Embedding.CosineDistance(vector)).SingleAsync(ct);
        Assert.Equal(0, distance);
        await transaction.RollbackAsync(ct);
    }

    [Fact]
    public async Task RolDeAplicacionNoPuedeBorrarUsuariosNiModificarLedger()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.CreateContext();
        var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM usuarios WHERE false", ct));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
        error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync("UPDATE movimientos_financieros SET monto=monto WHERE false", ct));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
    }

    [Fact]
    public async Task JsonYClaveCompuestaSeMapeanSinColumnasAdicionales()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.CreateContext();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        using var payload = JsonDocument.Parse("{\"contenido\":\"prueba\"}");
        var outbox = new OutboxMensajeRow { Agregado = "Prueba", AgregadoId = "1", Tipo = "prueba", Payload = payload };
        db.OutboxMensajes.Add(outbox);
        await db.SaveChangesAsync(ct);
        var mensaje = new MensajeProcesadoRow { MensajeId = outbox.Id, Consumidor = "Prueba" };
        db.MensajesProcesados.Add(mensaje);
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
        var stored = await db.MensajesProcesados.SingleAsync(m => m.MensajeId == mensaje.MensajeId && m.Consumidor == mensaje.Consumidor, ct);
        Assert.Equal(DateTimeKind.Utc, stored.Fecha.Kind);
        var persistedOutbox = await db.OutboxMensajes.SingleAsync(m => m.Id == outbox.Id, ct);
        Assert.Equal("prueba", persistedOutbox.Payload.RootElement.GetProperty("contenido").GetString());
        await transaction.RollbackAsync(ct);
    }
}
