using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using Promotora.WebApi.Controllers;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Promotora.WebApi.Ticketing;

public sealed class CompraExamplesFilter : ISchemaFilter
{
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type == typeof(CrearCompraRequest) && schema is OpenApiSchema concrete)
            concrete.Example = JsonNode.Parse("""{"eventoId":1,"items":[{"tipoEntradaId":1,"cantidad":2}]}""");
    }
}
