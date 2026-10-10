using System.Security.Cryptography;
using System.Text;
using Promotora.Application.Ticketing.Ports;
using Promotora.Domain.Shared;

namespace Promotora.Infrastructure.Ticketing;

public sealed class HmacTicketTokenService : ITicketTokenService
{
    private readonly byte[]? _key;

    public HmacTicketTokenService(byte[]? key)
    {
        if (key is not null && key.Length < 32) throw new ArgumentException("La clave QR requiere al menos 32 bytes.", nameof(key));
        _key = key?.ToArray();
    }

    public Result<string> Firmar(Guid boletoPublicId)
    {
        if (_key is null) return Result<string>.Failure(new Error("boleto.configuracion", "Falta la clave externa de firma de boletos.", ErrorType.BusinessRule));
        if (boletoPublicId == Guid.Empty) return Result<string>.Failure(new Error("boleto.identificador", "Identificador de boleto inválido.", ErrorType.Validation));
        string mensaje = $"v1.{boletoPublicId:N}";
        return Result<string>.Success($"{mensaje}.{Base64Url(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(mensaje)))}");
    }

    public bool Validar(string? token, out Guid boletoPublicId)
    {
        boletoPublicId = Guid.Empty;
        // Longitud exacta limita trabajo y garantiza compatibilidad con VARCHAR(100).
        if (_key is null || token is null || token.Length != 79) return false;
        var partes = token.Split('.');
        if (partes.Length != 3 || partes[0] != "v1" || !Guid.TryParseExact(partes[1], "N", out var id) || id == Guid.Empty || partes[1] != id.ToString("N")) return false;
        byte[] firma;
        try { firma = Convert.FromBase64String(partes[2].Replace('-', '+').Replace('_', '/') + "="); }
        catch (FormatException) { return false; }
        if (firma.Length != 32 || Base64Url(firma) != partes[2]) return false;
        var esperado = HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes($"v1.{partes[1]}"));
        if (!CryptographicOperations.FixedTimeEquals(esperado, firma)) return false;
        boletoPublicId = id;
        return true;
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
