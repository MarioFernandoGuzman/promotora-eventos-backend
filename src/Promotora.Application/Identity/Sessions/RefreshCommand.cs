using Promotora.Application.Shared.Handlers;

namespace Promotora.Application.Identity.Sessions;

public sealed record RefreshCommand(string RefreshToken) : ICommand<TokenDto>
{
    public override string ToString() => nameof(RefreshCommand);
}
