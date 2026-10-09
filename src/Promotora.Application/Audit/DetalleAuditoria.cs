namespace Promotora.Application.Audit;

// Lista acotada de datos: no acepta bodies, contraseñas, hashes ni tokens.
public sealed record DetalleAuditoria(MotivoAuditoria Motivo, Guid? FamiliaId = null);
