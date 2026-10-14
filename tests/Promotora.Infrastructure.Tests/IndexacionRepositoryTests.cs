using Microsoft.EntityFrameworkCore;
using Promotora.Application.AI.Documentos;
using Promotora.Infrastructure.AI.Persistence;

namespace Promotora.Infrastructure.Tests;

public sealed class IndexacionRepositoryTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task PublicacionConservaHistoriaExcluyeConcurrentesYRevierteIndiceParcial()
    {
        var ct = TestContext.Current.CancellationToken; await using var db = postgres.CreateContext();
        var datos = await CompraFixture.CrearAsync(db, 1, ct);
        int usuario = await db.Clientes.Where(c => c.Id == datos.ClienteId).Select(c => c.UsuarioId).SingleAsync(ct);
        string serie = "db:documentos-rag/" + Guid.NewGuid().ToString("N");
        var primero = new DocumentoRagRow { Titulo = "FAQ", Contenido = "Acceso público", TipoDocumento = "MD", Estado = "PENDIENTE", Version = 1, RutaArchivo = serie, SubidoPor = usuario };
        db.DocumentosRag.Add(primero); await db.SaveChangesAsync(ct);
        var repo = new EfIndexacionRepository(db); var original = (await repo.ObtenerAsync(primero.Id, ct))!;
        await using (var lease = await repo.IntentarBloquearAsync(primero.Id, ct))
        { Assert.NotNull(lease); Assert.Null(await repo.IntentarBloquearAsync(primero.Id, ct)); }
        float[] vector = new float[1536]; vector[0] = 1;
        var chunks = new[] { new FragmentoVectorizado(new(1, 2, "Acceso público"), vector, "gemini-embedding-2") };
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        { Assert.True(await repo.PublicarAsync(original, chunks, ct)); await tx.CommitAsync(ct); }
        var segundo = new DocumentoRagRow { Titulo = "FAQ v2", Contenido = "Acceso actualizado", TipoDocumento = "MD", Estado = "PENDIENTE", Version = 2, RutaArchivo = serie, SubidoPor = usuario };
        db.DocumentosRag.Add(segundo); await db.SaveChangesAsync(ct);
        var nuevo = (await repo.ObtenerAsync(segundo.Id, ct))!;
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        { Assert.True(await repo.PublicarAsync(nuevo, chunks, ct)); await tx.RollbackAsync(ct); }
        db.ChangeTracker.Clear();
        Assert.Equal("INDEXADO", (await repo.ObtenerAsync(primero.Id, ct))!.Documento.Estado);
        Assert.False(await db.ChunksRag.AnyAsync(c => c.DocumentoId == segundo.Id, ct));
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        { Assert.True(await repo.PublicarAsync(nuevo, chunks, ct)); await tx.CommitAsync(ct); }
        Assert.Equal("INACTIVO", (await repo.ObtenerAsync(primero.Id, ct))!.Documento.Estado);
        Assert.Equal("INDEXADO", (await repo.ObtenerAsync(segundo.Id, ct))!.Documento.Estado);
        Assert.Equal(1, await db.ChunksRag.CountAsync(c => c.DocumentoId == segundo.Id, ct));
        await repo.MarcarErrorAsync(segundo.Id, ct);
        Assert.Equal("INDEXADO", (await repo.ObtenerAsync(segundo.Id, ct))!.Documento.Estado);
    }
}
