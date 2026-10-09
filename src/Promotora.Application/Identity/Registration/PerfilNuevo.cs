namespace Promotora.Application.Identity.Registration;

public sealed record PerfilNuevo(DateOnly? FechaNacimiento = null, string? Telefono = null,
    string? Empresa = null, string? Nit = null, string NivelAcceso = "ESTANDAR");
