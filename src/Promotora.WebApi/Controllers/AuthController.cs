using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Promotora.Application.Identity.Registration;
using Promotora.Application.Identity.Sessions;
using Promotora.Application.Shared.Handlers;
using Promotora.Domain.Identity;
using Promotora.Domain.Shared;
using Promotora.WebApi.Identity;

namespace Promotora.WebApi.Controllers;

public sealed class AuthController(ICommandHandler<CrearUsuarioCommand, UsuarioDto> crear,
    ICommandHandler<LoginCommand, TokenDto> login,
    ICommandHandler<RefreshCommand, TokenDto> refresh,
    ICommandHandler<LogoutCommand, Unit> logout) : ApiControllerBase
{
    [HttpPost("registro")]
    [EnableRateLimiting(IdentityHttpProtection.Registro)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [AllowAnonymous]
    [ProducesResponseType<UsuarioDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UsuarioDto>> Registro(RegistroRequest request, CancellationToken ct) =>
        FromResult(await crear.HandleAsync(new CrearUsuarioCommand(request.Email, request.Password, request.Nombre,
            request.Apellido, RolUsuario.CLIENTE, new PerfilNuevo(request.FechaNacimiento, request.Telefono)), ct));

    [HttpPost("login")]
    [EnableRateLimiting(IdentityHttpProtection.Login)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [AllowAnonymous]
    [ProducesResponseType<TokenDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TokenDto>> Login(LoginCommand request, CancellationToken ct) => FromResult(await login.HandleAsync(request, ct));

    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType<TokenDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TokenDto>> Refresh(RefreshCommand request, CancellationToken ct) => FromResult(await refresh.HandleAsync(request, ct));

    [Authorize(Policy = IdentityPolicies.SesionCompleta)]
    [HttpPost("logout")]
    [ProducesResponseType<Unit>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<Unit>> Logout(LogoutCommand request, CancellationToken ct) => FromResult(await logout.HandleAsync(request, ct));
}
