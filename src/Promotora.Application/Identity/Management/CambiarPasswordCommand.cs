using Promotora.Application.Shared.Handlers;
using Promotora.Domain.Shared;

namespace Promotora.Application.Identity.Management;

public sealed record CambiarPasswordCommand(int Id, string PasswordActual, string PasswordNuevo) : ICommand<Unit>
{
    public override string ToString() => nameof(CambiarPasswordCommand);
}
