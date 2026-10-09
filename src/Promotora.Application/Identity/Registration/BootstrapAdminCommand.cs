using Promotora.Application.Shared.Handlers;

namespace Promotora.Application.Identity.Registration;

public sealed record BootstrapAdminCommand(string Email, string Password) : ICommand<UsuarioDto>
{
    public override string ToString() => nameof(BootstrapAdminCommand);
}
