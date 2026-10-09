using Microsoft.AspNetCore.Mvc;
using Promotora.Application.Identity.Registration;
using Promotora.Application.Shared.Handlers;
using Promotora.Domain.Identity;
using Promotora.WebApi.Identity;

namespace Promotora.WebApi.Controllers;

public sealed class AuthController(ICommandHandler<CrearUsuarioCommand, UsuarioDto> crear) : ApiControllerBase
{
    [HttpPost("registro")]
    [ProducesResponseType<UsuarioDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UsuarioDto>> Registro(RegistroRequest request, CancellationToken ct) =>
        FromResult(await crear.HandleAsync(new CrearUsuarioCommand(request.Email, request.Password, request.Nombre,
            request.Apellido, RolUsuario.CLIENTE, new PerfilNuevo(request.FechaNacimiento, request.Telefono)), ct));
}
