using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Promotora.WebApi.Catalog;

public sealed class CatalogExamplesFilter : ISchemaFilter
{
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        if (schema is not OpenApiSchema concrete) return;
        string? json = context.Type == typeof(CategoriaEventoRequest) ? """
            {"nombre":"Festival","descripcion":"Eventos de festival"}
            """ : context.Type == typeof(PresentadorRequest) ? """
            {"nombre":"Artista de prueba","tipo":"ARTISTA","biografia":null,"imagenUrl":null}
            """ : null;
        if (json is not null) concrete.Example = JsonNode.Parse(json);
    }
}
