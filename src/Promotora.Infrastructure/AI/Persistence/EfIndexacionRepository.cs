using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql;
using Pgvector;
using Promotora.Application.AI.Documentos;
using Promotora.Infrastructure.Shared.Persistence;

namespace Promotora.Infrastructure.AI.Persistence;

public sealed class EfIndexacionRepository(PromotoraDbContext db) : IIndexacionRepository
{
    public async Task<DocumentoParaIndexar?> ObtenerAsync(int id, CancellationToken ct)
    {
        var d = await db.DocumentosRag.AsNoTracking().SingleOrDefaultAsync(d => d.Id == id, ct);
        return d is null ? null : new(new(d.Id, d.Titulo, d.TipoDocumento, d.Version, d.Estado, d.FechaCarga, d.SubidoPor), d.Contenido ?? "", d.RutaArchivo);
    }
    public async Task<IAsyncDisposable?> IntentarBloquearAsync(int id, CancellationToken ct)
    {
        // La cadena de una conexión ya abierta omite su password (PersistSecurityInfo=false).
        // Usar las opciones originales, sin habilitar divulgación de credenciales.
        string? cadena = db.GetService<IDbContextOptions>().Extensions.OfType<RelationalOptionsExtension>().Single().ConnectionString;
        var conexion = new NpgsqlConnection(cadena);
        try
        {
            await conexion.OpenAsync(ct);
            await using var comando = new NpgsqlCommand("SELECT pg_try_advisory_lock(9104, @id)", conexion);
            comando.Parameters.AddWithValue("id", id);
            if (await comando.ExecuteScalarAsync(ct) is true) return new Bloqueo(conexion, id);
            await conexion.DisposeAsync(); return null;
        }
        catch { await conexion.DisposeAsync(); throw; }
    }
    public async Task<bool> PublicarAsync(DocumentoParaIndexar documento, IReadOnlyList<FragmentoVectorizado> fragmentos, CancellationToken ct)
    {
        var bloqueado = await new EfDocumentoRepository(db).BloquearSerieAsync(documento.Documento.Id, ct);
        if (bloqueado is null || bloqueado.Documento.Estado != documento.Documento.Estado || bloqueado.Documento.Estado == "INACTIVO") return false;
        if (documento.Referencia is not null && await db.DocumentosRag.Where(d => d.RutaArchivo == documento.Referencia).MaxAsync(d => d.Version, ct) != documento.Documento.Version) return false;
        // Lectura/modelo y publicación se confirman juntos; nunca se deja un índice parcial visible.
        await db.ChunksRag.Where(c => c.DocumentoId == documento.Documento.Id).ExecuteDeleteAsync(ct);
        foreach (var f in fragmentos)
            db.ChunksRag.Add(new ChunkRagRow
            {
                DocumentoId = documento.Documento.Id,
                Orden = f.Fragmento.Orden,
                Pagina = f.Fragmento.Pagina,
                Contenido = f.Fragmento.Contenido,
                Embedding = new Vector(f.Vector),
                Metadata = JsonSerializer.SerializeToDocument(new { modelo = f.Modelo })
            });
        await db.SaveChangesAsync(ct);
        if (documento.Referencia is not null)
            await db.DocumentosRag.Where(d => d.RutaArchivo == documento.Referencia && d.Id != documento.Documento.Id && d.Estado == "INDEXADO")
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.Estado, "INACTIVO"), ct);
        return await db.DocumentosRag.Where(d => d.Id == documento.Documento.Id && d.Estado == documento.Documento.Estado)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.Estado, "INDEXADO"), ct) == 1;
    }
    public async Task MarcarErrorAsync(int id, CancellationToken ct) => await db.DocumentosRag.Where(d => d.Id == id && d.Estado == "PENDIENTE")
        .ExecuteUpdateAsync(s => s.SetProperty(d => d.Estado, "ERROR"), ct);
    private sealed class Bloqueo(NpgsqlConnection conexion, int id) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await using var comando = new NpgsqlCommand("SELECT pg_advisory_unlock(9104, @id)", conexion);
                comando.Parameters.AddWithValue("id", id); await comando.ExecuteScalarAsync(limite.Token);
            }
            finally { await conexion.DisposeAsync(); }
        }
    }
}
