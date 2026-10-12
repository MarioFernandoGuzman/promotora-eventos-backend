using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using Promotora.Application.AI;
using Promotora.Application.AI.Ports;
using Promotora.Infrastructure.Shared.Persistence;

namespace Promotora.Infrastructure.AI.Persistence;

public sealed class EfVectorSearchAdapter(PromotoraDbContext db) : IVectorSearchPort
{
    public async Task<IReadOnlyList<FragmentoRecuperado>> BuscarAsync(float[] embedding, int limite, double similitudMinima, CancellationToken ct)
    {
        if (embedding.Length != PoliticaAsistente.Dimensiones || embedding.Any(n => !float.IsFinite(n)) || embedding.All(n => n == 0))
            throw new ArgumentException("El vector debe tener 1536 componentes finitos y norma distinta de cero.", nameof(embedding));
        if (limite is < 1 or > 10) throw new ArgumentOutOfRangeException(nameof(limite));
        if (!double.IsFinite(similitudMinima) || similitudMinima is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(similitudMinima));
        var vector = new Vector(embedding);
        var filas = await (from chunk in db.ChunksRag.AsNoTracking()
                           join documento in db.DocumentosRag.AsNoTracking() on chunk.DocumentoId equals documento.Id
                           where documento.Estado == "INDEXADO"
                           let distancia = chunk.Embedding.CosineDistance(vector)
                           where distancia <= 1 - similitudMinima
                           orderby distancia, chunk.Id
                           select new { chunk.Id, chunk.DocumentoId, documento.Titulo, chunk.Pagina, chunk.Contenido, Distancia = distancia })
            .Take(limite).ToListAsync(ct);
        return filas.Select(f => new FragmentoRecuperado(f.Id, f.DocumentoId, f.Titulo, f.Pagina, f.Contenido, 1 - f.Distancia)).ToArray();
    }
}
