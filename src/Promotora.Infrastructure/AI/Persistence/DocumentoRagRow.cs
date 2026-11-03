namespace Promotora.Infrastructure.AI.Persistence;

// Modelo de persistencia del esquema Flyway; no representa reglas del dominio.
public sealed class DocumentoRagRow
{
    public int Id { get; set; }
    public string Titulo { get; set; } = null!;
    public string? TipoDocumento { get; set; }
    public string? Contenido { get; set; }
    public string? RutaArchivo { get; set; }
    public int Version { get; set; } = 1;
    public DateTime FechaCarga { get; set; }
    public string Estado { get; set; } = null!;
    public int SubidoPor { get; set; }
}
