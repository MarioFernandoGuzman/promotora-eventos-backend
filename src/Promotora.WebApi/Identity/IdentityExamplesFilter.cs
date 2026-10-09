using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using Promotora.Application.Identity.Sessions;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Promotora.WebApi.Identity;

public sealed class IdentityExamplesFilter : ISchemaFilter
{
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        if (schema is not OpenApiSchema concreteSchema)
        {
            return;
        }

        string? json = context.Type == typeof(RegistroRequest) ? """
            {"email":"cliente@example.test","password":"<elige-tu-password-local>","nombre":"Cliente","apellido":"Prueba","fechaNacimiento":null,"telefono":null}
            """ : context.Type == typeof(LoginCommand) ? """
            {"email":"admin@example.test","password":"<tu-password-local>"}
            """ : context.Type == typeof(AltaAdministradorRequest) ? """
            {"email":"administrador@example.test","password":"<elige-tu-password-local>","nombre":"Administrador","apellido":"Prueba"}
            """ : context.Type == typeof(AltaDuenoRequest) ? """
            {"email":"dueno@example.test","password":"<elige-tu-password-local>","nombre":"Dueño","apellido":"Prueba","empresa":"Empresa","nit":null}
            """ : context.Type == typeof(ActualizarUsuarioRequest) ? """
            {"email":"cliente@example.test","nombre":"Cliente","apellido":"Prueba","fechaNacimiento":null,"telefono":null,"empresa":null,"nit":null}
            """ : context.Type == typeof(CambiarPasswordRequest) ? """
            {"passwordActual":"<tu-password-actual>","passwordNuevo":"<elige-un-password-diferente>"}
            """ : null;
        if (json is not null)
        {
            concreteSchema.Example = JsonNode.Parse(json);
        }
    }

}
