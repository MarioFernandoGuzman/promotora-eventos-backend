using Promotora.Application.Shared.Handlers;

namespace Promotora.Application.Identity.Sessions;

public sealed record LoginCommand(string Email, string Password) : ICommand<TokenDto>
{
    public override string ToString() => nameof(LoginCommand);
}
