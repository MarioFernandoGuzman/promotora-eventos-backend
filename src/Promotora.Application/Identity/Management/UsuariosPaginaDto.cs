using Promotora.Application.Identity.Registration;

namespace Promotora.Application.Identity.Management;

public sealed record UsuariosPaginaDto(IReadOnlyList<UsuarioDto> Items, int Page, int PageSize, int Total);
