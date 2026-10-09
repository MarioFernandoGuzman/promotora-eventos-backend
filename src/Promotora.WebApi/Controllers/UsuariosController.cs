using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Promotora.Application.Identity.Registration;
using Promotora.Application.Shared.Handlers;
using Promotora.Domain.Identity;
using Promotora.WebApi.Identity;

namespace Promotora.WebApi.Controllers;

public sealed class UsuariosController(ICommandHandler<CrearUsuarioCommand, UsuarioDto> crear) : ApiControllerBase
{
    [HttpPost("administradores")]
    [Authorize(Policy = IdentityPolicies.Administrador)]
    [ProducesResponseType<UsuarioDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UsuarioDto>> Administrador(AltaAdministradorRequest request, CancellationToken ct) =>
        FromResult(await crear.HandleAsync(new CrearUsuarioCommand(request.Email, request.Password, request.Nombre,
            request.Apellido, RolUsuario.ADMINISTRADOR, new PerfilNuevo()), ct));

    [HttpPost("duenos-recinto")]
    [Authorize(Policy = IdentityPolicies.Administrador)]
    [ProducesResponseType<UsuarioDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UsuarioDto>> Dueno(AltaDuenoRequest request, CancellationToken ct) =>
        FromResult(await crear.HandleAsync(new CrearUsuarioCommand(request.Email, request.Password, request.Nombre,
            request.Apellido, RolUsuario.DUENO_RECINTO, new PerfilNuevo(Empresa: request.Empresa, Nit: request.Nit)), ct));
}
