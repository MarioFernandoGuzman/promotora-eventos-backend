using Microsoft.OpenApi;
using Promotora.WebApi.Controllers;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Promotora.WebApi.Reporting;

public sealed class ReportesOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.MethodInfo.DeclaringType != typeof(ReportesController)) return;
        if (operation.Responses?["200"] is not OpenApiResponse response) return;
        response.Content = new Dictionary<string, OpenApiMediaType>
        {
            ["application/json"] = new() { Schema = context.SchemaGenerator.GenerateSchema(typeof(Promotora.Application.Reporting.ReporteDto), context.SchemaRepository) },
            ["application/pdf"] = new() { Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" } },
            ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"] = new() { Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" } }
        };
    }
}
