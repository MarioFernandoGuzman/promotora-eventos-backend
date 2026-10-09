using Microsoft.EntityFrameworkCore;
using Npgsql;
using Promotora.Application.Identity.Management;
using Promotora.Application.Identity.Ports;
using Promotora.Application.Identity.Registration;

namespace Promotora.Infrastructure.Identity.Persistence;

public sealed partial class EfUsuarioRepository
{
    public async Task<UsuarioDetalleDto?> GetDetailAsync(int id, CancellationToken ct)
    {
        var user = await db.Usuarios.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (user is null)
        {
            return null;
        }

        PerfilDto profile;
        switch (user.Rol)
        {
            case "CLIENTE":
                var cliente = await db.Clientes.AsNoTracking().SingleAsync(x => x.UsuarioId == id, ct);
                profile = new PerfilDto(cliente.FechaNacimiento, cliente.Telefono, null, null, null);
                break;
            case "ADMINISTRADOR":
                var admin = await db.Administradores.AsNoTracking().SingleAsync(x => x.UsuarioId == id, ct);
                profile = new PerfilDto(null, null, null, null, admin.NivelAcceso);
                break;
            case "DUENO_RECINTO":
                var dueno = await db.DuenosRecinto.AsNoTracking().SingleAsync(x => x.UsuarioId == id, ct);
                profile = new PerfilDto(null, null, dueno.Empresa, dueno.Nit, null);
                break;
            default:
                throw new InvalidOperationException("Perfil desconocido.");
        }

        return new UsuarioDetalleDto(user.ToDto(), profile);
    }

    public async Task<UsuariosPaginaDto> ListAsync(int page, int pageSize, CancellationToken ct)
    {
        int total = await db.Usuarios.CountAsync(ct);
        var rows = await db.Usuarios.AsNoTracking().OrderBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new UsuariosPaginaDto(rows.Select(x => x.ToDto()).ToArray(), page, pageSize, total);
    }

    public async Task UpdateAsync(int id, string email, string nombre, string apellido, PerfilNuevo perfil, CancellationToken ct)
    {
        try
        {
            await db.Usuarios.Where(x => x.Id == id).ExecuteUpdateAsync(setter => setter
                .SetProperty(x => x.Email, email).SetProperty(x => x.Nombre, nombre).SetProperty(x => x.Apellido, apellido), ct);
        }
        catch (PostgresException exception) when (exception is { SqlState: "23505", ConstraintName: "uq_usuarios_email" })
        {
            throw new UsuarioEmailDuplicadoException();
        }

        await db.Clientes.Where(x => x.UsuarioId == id).ExecuteUpdateAsync(setter => setter
            .SetProperty(x => x.FechaNacimiento, perfil.FechaNacimiento).SetProperty(x => x.Telefono, perfil.Telefono), ct);
        if (perfil.Empresa is not null)
        {
            await db.DuenosRecinto.Where(x => x.UsuarioId == id).ExecuteUpdateAsync(setter => setter
                .SetProperty(x => x.Empresa, perfil.Empresa.Trim()).SetProperty(x => x.Nit, perfil.Nit), ct);
        }
    }

    public async Task<bool> DeactivateAsync(int id, string expectedState, CancellationToken ct) =>
        await db.Usuarios.Where(x => x.Id == id && x.Estado == expectedState)
            .ExecuteUpdateAsync(setter => setter.SetProperty(x => x.Estado, "INACTIVO"), ct) == 1;

    public async Task ChangePasswordAsync(int id, string hash, CancellationToken ct) =>
        await db.Usuarios.Where(x => x.Id == id).ExecuteUpdateAsync(setter => setter
            .SetProperty(x => x.PasswordHash, hash).SetProperty(x => x.DebeCambiarPassword, false), ct);
}
