namespace Promotora.Application.Identity.Management;

public sealed record PerfilDto(DateOnly? FechaNacimiento, string? Telefono, string? Empresa, string? Nit, string? NivelAcceso);
