using Promotora.Application.Shared.Handlers;
using Promotora.Domain.Shared;

namespace Promotora.Application.Identity.Sessions;

public sealed record LogoutCommand(string RefreshToken) : ICommand<Unit>
{
    public override string ToString() => nameof(LogoutCommand);
}
