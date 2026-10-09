using System.Globalization;
using Promotora.Application.Audit;
using Promotora.Application.Identity.Ports;
using Promotora.Application.Shared.Handlers;
using Promotora.Application.Shared.Ports;
using Promotora.Domain.Shared;

namespace Promotora.Application.Identity.Sessions;

public sealed class LoginHandler(IUsuarioRepository usuarios, ISesionRepository sesiones, IPasswordService passwords,
    ITokenService tokens, IUsuarioActual actual, IClock clock, SesionOptions options, IAuditLogger audit) : ICommandHandler<LoginCommand, TokenDto>
{
    public Task<Result<TokenDto>> HandleAsync(LoginCommand request, CancellationToken ct) => sesiones.ExecuteAtomicAsync(async token =>
    {
        var usuario = await usuarios.FindByEmailAsync(request.Email.Trim().ToLowerInvariant(), token);
        var validPassword = passwords.Verify(usuario?.PasswordHash, request.Password);
        if (usuario is null || !validPassword || !usuario.PuedeAutenticarse)
        {
            await audit.WriteAsync(usuario?.Id, "LOGIN_FAILED", "usuarios", usuario?.Id.ToString(CultureInfo.InvariantCulture), actual.IpOrigen, token);
            return Result<TokenDto>.Failure(SesionErrors.Invalid);
        }

        await sesiones.LockUsuarioAsync(usuario.Id, token);
        usuario = (await usuarios.FindByIdAsync(usuario.Id, token))!;
        // Releer tras el lock evita sesiones emitidas con contraseña/estado anteriores.
        if (!usuario.PuedeAutenticarse || !passwords.Verify(usuario.PasswordHash, request.Password))
        {
            await audit.WriteAsync(usuario.Id, "LOGIN_FAILED", "usuarios", usuario.Id.ToString(CultureInfo.InvariantCulture), actual.IpOrigen, token);
            return Result<TokenDto>.Failure(SesionErrors.Invalid);
        }

        var refresh = tokens.CreateRefreshToken();
        var accessExpiry = clock.UtcNow.AddMinutes(usuario.DebeCambiarPassword ? 5 : options.AccessMinutes);
        var sessionId = await sesiones.AddAsync(usuario.Id, tokens.HashRefreshToken(refresh), Guid.NewGuid(),
            usuario.DebeCambiarPassword ? accessExpiry : clock.UtcNow.AddDays(options.RefreshDays), actual.IpOrigen, token);
        await audit.WriteAsync(usuario.Id, "LOGIN", "usuarios", usuario.Id.ToString(CultureInfo.InvariantCulture), actual.IpOrigen, token);
        return Result<TokenDto>.Success(new TokenDto(tokens.CreateAccessToken(usuario, sessionId, accessExpiry),
            usuario.DebeCambiarPassword ? null : refresh, accessExpiry, usuario.DebeCambiarPassword, usuario.Id));
    }, ct);
}
