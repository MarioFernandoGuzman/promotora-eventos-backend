using Promotora.Application.Shared.Handlers;
using Promotora.Domain.Shared;

namespace Promotora.Application.Identity.Management;

public sealed record BajaUsuarioCommand(int Id) : ICommand<Unit>;
