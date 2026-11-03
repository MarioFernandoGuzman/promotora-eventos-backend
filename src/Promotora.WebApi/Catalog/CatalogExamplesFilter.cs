using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using Promotora.WebApi.Controllers;
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
            """ : context.Type == typeof(EventoRequest) ? """
            {"recintoId":1,"categoriaId":1,"nombre":"Festival","fechaInicio":"2027-06-01T18:00:00Z","fechaFin":"2027-06-01T23:00:00Z","fechaInicioVenta":"2026-10-01T00:00:00Z","fechaFinVenta":"2027-06-01T17:00:00Z","maxBoletosPorCompra":5}
            """ : context.Type == typeof(EstadoEventoRequest) ? """
            {"estadoEsperado":"BORRADOR","estadoNuevo":"PROGRAMADO"}
            """ : context.Type == typeof(AsociarPresentadorRequest) ? """
            {"presentadorId":1,"rol":"HEADLINER"}
            """ : context.Type == typeof(RolPresentadorRequest) ? """
            {"rol":"INVITADO"}
            """ : context.Type == typeof(CrearTipoEntradaRequest) ? """
            {"nombre":"General","precio":150.00,"cupoTotal":100,"localidadId":null}
            """ : context.Type == typeof(EditarTipoEntradaRequest) ? """
            {"nombre":"General","precio":175.00,"cupoTotal":80}
            """ : null;
        if (json is not null) concrete.Example = JsonNode.Parse(json);
    }
}
