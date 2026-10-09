using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Promotora.Application.Audit;
using Promotora.Application.Identity.Ports;
using Promotora.Application.Identity.Registration;
using Promotora.Application.Shared.Ports;
using Promotora.Domain.Identity;
using Promotora.Domain.Shared;
using Promotora.Infrastructure.Shared.Persistence;

namespace Promotora.Infrastructure.Identity.Persistence;

public sealed class EfUsuarioRepository(PromotoraDbContext db, IUnitOfWork unitOfWork, IAuditLogger audit) : IUsuarioRepository
{
    public async Task<Usuario?> FindByIdAsync(int id, CancellationToken ct) =>
        (await db.Usuarios.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct))?.ToDomain();

    public async Task<Usuario?> FindByEmailAsync(string email, CancellationToken ct) =>
        (await db.Usuarios.AsNoTracking().SingleOrDefaultAsync(x => x.Email == email, ct))?.ToDomain();

    public async Task<Result<UsuarioDto>> CreateAsync(Usuario usuario, PerfilNuevo perfil, int? actorId, string? ip, bool bootstrap, CancellationToken ct)
    {
        try
        {
            return await unitOfWork.ExecuteInTransactionAsync(async token =>
            {
                if (bootstrap)
                {
                    // Lock de transacción compartido por todas las instancias, sin nuevas tablas.
                    await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(730301)", token);
                    var existing = await (from admin in db.Administradores
                                          join user in db.Usuarios on admin.UsuarioId equals user.Id
                                          where admin.NivelAcceso == "SUPERADMIN"
                                          select user).AsNoTracking().FirstOrDefaultAsync(token);
                    if (existing is not null)
                    {
                        return Result<UsuarioDto>.Success(existing.ToDto());
                    }
                }

                var row = new UsuarioRow
                {
                    Email = usuario.Email,
                    PasswordHash = usuario.PasswordHash,
                    Nombre = usuario.Nombre,
                    Apellido = usuario.Apellido,
                    Rol = usuario.Rol.ToString(),
                    Estado = usuario.Estado,
                    DebeCambiarPassword = usuario.DebeCambiarPassword,
                    FechaRegistro = usuario.FechaRegistro
                };
                db.Usuarios.Add(row);
                await db.SaveChangesAsync(token);
                switch (usuario.Rol)
                {
                    case RolUsuario.CLIENTE:
                        db.Clientes.Add(new ClienteRow { UsuarioId = row.Id, FechaNacimiento = perfil.FechaNacimiento, Telefono = perfil.Telefono });
                        break;
                    case RolUsuario.ADMINISTRADOR:
                        db.Administradores.Add(new AdministradorRow { UsuarioId = row.Id, NivelAcceso = perfil.NivelAcceso });
                        break;
                    case RolUsuario.DUENO_RECINTO:
                        db.DuenosRecinto.Add(new DuenoRecintoRow { UsuarioId = row.Id, Empresa = perfil.Empresa!, Nit = perfil.Nit });
                        break;
                    default:
                        throw new InvalidOperationException("Rol desconocido.");
                }

                await audit.WriteAsync(actorId, "CREATE", "usuarios",
                    row.Id.ToString(CultureInfo.InvariantCulture), ip, token);
                if (usuario.Rol == RolUsuario.CLIENTE)
                {
                    db.OutboxMensajes.Add(new OutboxMensajeRow
                    {
                        Agregado = "usuarios",
                        AgregadoId = row.Id.ToString(CultureInfo.InvariantCulture),
                        Tipo = "usuario.registrado",
                        Payload = JsonSerializer.SerializeToDocument(new { usuarioId = row.Id, email = row.Email }),
                        Fecha = usuario.FechaRegistro
                    });
                }

                return Result<UsuarioDto>.Success(row.ToDto());
            }, ct);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: "23505", ConstraintName: "uq_usuarios_email" })
        {
            return Result<UsuarioDto>.Failure(new Error("identity.email_duplicate", "El correo ya está registrado.", ErrorType.Conflict));
        }
    }
}
