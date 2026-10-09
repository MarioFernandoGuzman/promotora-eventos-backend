using System.Text.Json;
using Pgvector;

namespace Promotora.Infrastructure.AI.Persistence;

// Modelo de persistencia del esquema Flyway; no representa reglas del dominio.
public sealed class ChunkRagRow
{
    public long Id { get; set; }
    public int DocumentoId { get; set; }
    public int Orden { get; set; }
    public int? Pagina { get; set; }
    public string Contenido { get; set; } = null!;
    public Vector Embedding { get; set; } = null!;
    public JsonDocument? Metadata { get; set; }
}
