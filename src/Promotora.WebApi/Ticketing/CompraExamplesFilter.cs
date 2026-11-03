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
        if (context.Type == typeof(Promotora.Domain.Ticketing.DatosAsistente) && schema is OpenApiSchema asistente)
            asistente.Example = JsonNode.Parse("""{"nombreAsistente":"Persona de prueba","documentoIdentidad":"DOCUMENTO-PRUEBA","datosExtraJson":"{\"talla\":\"M\"}"}""");
        if (context.Type == typeof(ValidarBoletoRequest) && schema is OpenApiSchema boleto)
            boleto.Example = JsonNode.Parse("""{"codigoQr":"<copiar codigoQr de un boleto propio emitido>"}""");
    }
}
