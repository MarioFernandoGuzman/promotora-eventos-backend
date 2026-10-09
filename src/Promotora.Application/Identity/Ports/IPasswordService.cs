namespace Promotora.Application.Identity.Ports;

public interface IPasswordService
{
    string Hash(string password);
    bool Verify(string? hash, string password);
}
