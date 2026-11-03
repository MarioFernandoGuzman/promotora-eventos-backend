namespace Promotora.Infrastructure.Reporting;

// Identificadores y expresiones SQL exclusivamente internos. Nunca se reciben del cliente.
internal sealed record CampoReporte(string Columna, string Campo, string Titulo, string Tipo = "texto");
internal sealed record MetricaReporte(string Expresion, string Campo, string Titulo, string Tipo = "numero", string? TotalExpresion = null);
internal sealed record SeccionDefinition(string Codigo, string Vista, CampoReporte[] Dimensiones,
    MetricaReporte[] Metricas, string[] Filtros, string? EstadoColumna = "evento_estado", string? Orden = null);
internal sealed record ReporteDefinition(string Codigo, string Titulo, string Nota, SeccionDefinition[] Secciones);

internal static partial class ReporteCatalogo
{
    internal static IReadOnlyDictionary<string, ReporteDefinition> Crear()
    {
        var reportes = new Dictionary<string, ReporteDefinition>(StringComparer.Ordinal);
        AgregarVentas(reportes);
        AgregarFinanzas(reportes);
        AgregarOperacion(reportes);
        return reportes;
    }

    static partial void AgregarVentas(Dictionary<string, ReporteDefinition> reportes);
    static partial void AgregarFinanzas(Dictionary<string, ReporteDefinition> reportes);
    static partial void AgregarOperacion(Dictionary<string, ReporteDefinition> reportes);
}
