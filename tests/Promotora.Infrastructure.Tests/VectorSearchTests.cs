using Microsoft.EntityFrameworkCore;
using Pgvector;
using Promotora.Infrastructure.AI.Persistence;

namespace Promotora.Infrastructure.Tests;

public sealed class VectorSearchTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CosenoFiltraEstadoUmbralLimiteYCitaConPostgresReal()
    {
        await using var db = postgres.CreateContext();
        var datos = await CompraFixture.CrearAsync(db, 1, Ct);
        int usuario = await db.Clientes.Where(c => c.Id == datos.ClienteId).Select(c => c.UsuarioId).SingleAsync(Ct);
        var documentos = new[] { "INDEXADO", "PENDIENTE", "ERROR", "INACTIVO" }
            .Select(estado => new DocumentoRagRow { Titulo = "FAQ " + estado, Estado = estado, SubidoPor = usuario }).ToArray();
        db.DocumentosRag.AddRange(documentos); await db.SaveChangesAsync(Ct);
        float[] cercano = new float[1536]; cercano[0] = 1;
        float[] lejano = new float[1536]; lejano[1] = 1;
        foreach (var documento in documentos)
            db.ChunksRag.Add(new ChunkRagRow { DocumentoId = documento.Id, Orden = 1, Pagina = 2, Contenido = "Política de acceso", Embedding = new Vector(cercano) });
        db.ChunksRag.Add(new ChunkRagRow { DocumentoId = documentos[0].Id, Orden = 2, Contenido = "Distante", Embedding = new Vector(lejano) });
        await db.SaveChangesAsync(Ct);
        var adapter = new EfVectorSearchAdapter(db);
        var resultado = Assert.Single(await adapter.BuscarAsync(cercano, 5, 0.8, Ct));
        Assert.Equal(documentos[0].Id, resultado.DocumentoId);
        Assert.Equal(documentos[0].Titulo, resultado.Documento);
        Assert.Equal(2, resultado.Pagina);
        Assert.InRange(resultado.Similitud, 0.999, 1.001);
        Assert.Single(await adapter.BuscarAsync(cercano, 1, 0, Ct));
        Assert.Equal(2, (await adapter.BuscarAsync(cercano, 5, 0, Ct)).Count);
        await Assert.ThrowsAsync<ArgumentException>(() => adapter.BuscarAsync(new float[1024], 5, 0.8, Ct));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => adapter.BuscarAsync(cercano, 100, 0.8, Ct));
    }
}
