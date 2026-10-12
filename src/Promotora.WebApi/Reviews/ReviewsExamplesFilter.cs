using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using Promotora.WebApi.Controllers;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Promotora.WebApi.Reviews;

public sealed class ReviewsExamplesFilter : ISchemaFilter
{
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        if (schema is not OpenApiSchema concrete) return;
        string? json = context.Type == typeof(CrearResenaRequest) ? """{"eventoId":1,"calificacion":5,"comentario":"Excelente evento"}"""
            : context.Type == typeof(ResenasController.ModerarResenaRequest) ? """{"estadoNuevo":"APROBADA"}"""
            : context.Type == typeof(ResenasController.EditarResenaRequest) ? """{"calificacion":4,"comentario":"Nueva opinión","estadoEsperado":"APROBADA"}"""
            : context.Type == typeof(ResenasController.RetirarResenaRequest) ? """{"estadoEsperado":"APROBADA"}""" : null;
        if (json is not null) concrete.Example = JsonNode.Parse(json);
    }
}
