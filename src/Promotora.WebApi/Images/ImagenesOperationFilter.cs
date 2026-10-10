using Microsoft.OpenApi;
using Promotora.WebApi.Controllers;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Promotora.WebApi.Images;

public sealed class ImagenesOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.MethodInfo.DeclaringType != typeof(ImagenesController)
            || context.MethodInfo.Name != nameof(ImagenesController.Obtener)) return;
        if (operation.Responses?["200"] is OpenApiResponse response)
            response.Content = new Dictionary<string, OpenApiMediaType>
            {
                ["image/png"] = new() { Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" } }
            };
    }
}
