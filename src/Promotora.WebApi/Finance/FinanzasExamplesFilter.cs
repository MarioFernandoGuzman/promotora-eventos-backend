using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using Promotora.WebApi.Controllers;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Promotora.WebApi.Finance;

public sealed class FinanzasExamplesFilter : ISchemaFilter
{
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        if (schema is OpenApiSchema concrete && context.Type == typeof(CuentasController.LiquidarCuentaRequest))
            concrete.Example = JsonNode.Parse("""{"monto":100.00}""");
    }
}
