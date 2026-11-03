using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Promotora.Application.Identity.Ports;

namespace Promotora.Infrastructure.Identity;

public sealed class IdentityPasswordService : IPasswordService
{
    private readonly PasswordHasher<object> _hasher = new(Options.Create(new PasswordHasherOptions { IterationCount = 210_000 }));
    private readonly object _user = new();
    private readonly string _dummyHash;

    public IdentityPasswordService() => _dummyHash = _hasher.HashPassword(_user, Guid.NewGuid().ToString("N"));

    public string Hash(string password) => _hasher.HashPassword(_user, password);

    public bool Verify(string? hash, string password)
    {
        try
        {
            var result = _hasher.VerifyHashedPassword(_user, hash ?? _dummyHash, password);
            return hash is not null && result != PasswordVerificationResult.Failed;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
