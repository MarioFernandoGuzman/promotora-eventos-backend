using System.Globalization;
using Promotora.Application.Audit;
using Promotora.Application.Identity.Ports;
using Promotora.Application.Shared.Handlers;
using Promotora.Application.Shared.Ports;
using Promotora.Domain.Shared;

namespace Promotora.Application.Identity.Sessions;

public sealed class RefreshHandler(IUsuarioRepository usuarios, ISesionRepository sesiones, ITokenService tokens,
    IUsuarioActual actual, IClock clock, SesionOptions options, IAuditLogger audit) : ICommandHandler<RefreshCommand, TokenDto>
{
    public Task<Result<TokenDto>> HandleAsync(RefreshCommand request, CancellationToken ct) => sesiones.ExecuteAtomicAsync(async token =>
    {
        var hash = tokens.HashRefreshToken(request.RefreshToken);
        var refresh = await sesiones.FindAsync(hash, token);
        if (refresh is null)
        {
            return Result<TokenDto>.Failure(SesionErrors.Invalid);
        }

        await sesiones.LockUsuarioAsync(refresh.UsuarioId, token);
        refresh = (await sesiones.FindAsync(hash, token))!;
        if (refresh.RevocadoEn is not null)
        {
            await sesiones.RevokeFamilyAsync(refresh.FamiliaId, token);
            await audit.WriteAsync(refresh.UsuarioId, "LOGOUT", "refresh_tokens", refresh.Id.ToString(CultureInfo.InvariantCulture), actual.IpOrigen,
                new DetalleAuditoria(MotivoAuditoria.RefreshReutilizado, refresh.FamiliaId), token);
            return Result<TokenDto>.Failure(SesionErrors.Invalid);
        }

        var usuario = await usuarios.FindByIdAsync(refresh.UsuarioId, token);
        if (!refresh.PuedeRotar(clock.UtcNow) || usuario is not { PuedeAutenticarse: true, DebeCambiarPassword: false })
        {
            await sesiones.RevokeFamilyAsync(refresh.FamiliaId, token);
            await audit.WriteAsync(refresh.UsuarioId, "LOGOUT", "refresh_tokens", refresh.Id.ToString(CultureInfo.InvariantCulture), actual.IpOrigen,
                new DetalleAuditoria(MotivoAuditoria.SesionExpiradaOInhabilitada, refresh.FamiliaId), token);
            return Result<TokenDto>.Failure(SesionErrors.Invalid);
        }

        var next = tokens.CreateRefreshToken();
        var id = await sesiones.AddAsync(usuario.Id, tokens.HashRefreshToken(next), refresh.FamiliaId,
            clock.UtcNow.AddDays(options.RefreshDays), actual.IpOrigen, token);
        await sesiones.RevokeAsync(refresh.Id, id, token);
        var expiry = clock.UtcNow.AddMinutes(options.AccessMinutes);
        return Result<TokenDto>.Success(new TokenDto(tokens.CreateAccessToken(usuario, id, expiry), next, expiry, false, usuario.Id));
    }, ct);
}
