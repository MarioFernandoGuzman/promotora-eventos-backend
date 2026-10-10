using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using Promotora.Application.Venues;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Promotora.WebApi.Venues;

public sealed class VenuesExamplesFilter : ISchemaFilter
{
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        if (schema is not OpenApiSchema concrete)
        {
            return;
        }

        string? json = context.Type == typeof(CrearRecintoRequest) ? """
            {"duenoId":null,"nombre":"Recinto de prueba","direccion":"Avenida Principal","ciudad":"Guatemala","latitud":14.6349,"longitud":-90.5069,"imagenPlanoUrl":null,"capacidadTotal":500}
            """ : context.Type == typeof(ActualizarRecintoRequest) ? """
            {"nombre":"Recinto actualizado","direccion":"Avenida Principal","ciudad":"Guatemala","latitud":14.6349,"longitud":-90.5069,"imagenPlanoUrl":null,"capacidadTotal":500}
            """ : context.Type == typeof(DatosLocalidadDto) ? """
            {"nombre":"Palco","capacidad":100,"geometria":{"type":"polygon","points":[[10,20],[80,20],[80,60],[10,60]]},"color":"#3366CC"}
            """ : null;
        if (json is not null)
        {
            concrete.Example = JsonNode.Parse(json);
        }
    }
}
