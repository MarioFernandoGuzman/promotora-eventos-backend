using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Promotora.Application.Identity.Management;
using Promotora.Application.Identity.Registration;
using Promotora.Application.Shared.Handlers;
using Promotora.Domain.Identity;
using Promotora.Domain.Shared;
using Promotora.WebApi.Identity;

namespace Promotora.WebApi.Controllers;

public sealed class UsuariosController(ICommandHandler<CrearUsuarioCommand, UsuarioDto> crear,
    IQueryHandler<ObtenerUsuarioQuery, UsuarioDetalleDto> obtener,
    IQueryHandler<ListarUsuariosQuery, UsuariosPaginaDto> listar,
    ICommandHandler<ActualizarUsuarioCommand, UsuarioDetalleDto> actualizar,
    ICommandHandler<BajaUsuarioCommand, Unit> baja,
    ICommandHandler<CambiarPasswordCommand, Unit> password) : ApiControllerBase
{
    [HttpGet]
    [Authorize(Policy = IdentityPolicies.Administrador)]
    [ProducesResponseType<UsuariosPaginaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<UsuariosPaginaDto>> Listar([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        FromResult(await listar.HandleAsync(new ListarUsuariosQuery(page, pageSize), ct));

    [HttpGet("{id:int}")]
    [Authorize(Policy = IdentityPolicies.SesionCompleta)]
    [ProducesResponseType<UsuarioDetalleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UsuarioDetalleDto>> Obtener(int id, CancellationToken ct) => FromResult(await obtener.HandleAsync(new ObtenerUsuarioQuery(id), ct));

    [HttpPut("{id:int}")]
    [Authorize(Policy = IdentityPolicies.SesionCompleta)]
    [ProducesResponseType<UsuarioDetalleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<UsuarioDetalleDto>> Actualizar(int id, ActualizarUsuarioRequest request, CancellationToken ct) =>
        FromResult(await actualizar.HandleAsync(new ActualizarUsuarioCommand(id, request.Email, request.Nombre, request.Apellido,
            new PerfilNuevo(request.FechaNacimiento, request.Telefono, request.Empresa, request.Nit)), ct));

    [HttpDelete("{id:int}")]
    [Authorize(Policy = IdentityPolicies.SesionCompleta)]
    [ProducesResponseType<Unit>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<Unit>> Baja(int id, CancellationToken ct) => FromResult(await baja.HandleAsync(new BajaUsuarioCommand(id), ct));

    [HttpPut("{id:int}/password")]
    [Authorize(Policy = IdentityPolicies.CambioPassword)]
    [ProducesResponseType<Unit>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<Unit>> Password(int id, CambiarPasswordRequest request, CancellationToken ct) =>
        FromResult(await password.HandleAsync(new CambiarPasswordCommand(id, request.PasswordActual, request.PasswordNuevo), ct));

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
