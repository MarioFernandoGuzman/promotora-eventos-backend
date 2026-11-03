using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using Promotora.Application.Ticketing.Ports;
using Promotora.Infrastructure.Ticketing;

namespace Promotora.Infrastructure.Tests;

public sealed class HmacTicketTokenTests
{
    [Fact]
    public void FirmaCabeEnColumnaYValidaSinDependerDeJwt()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var service = new HmacTicketTokenService(key); var id = Guid.NewGuid();
        var firmado = service.Firmar(id);
        Assert.True(firmado.IsSuccess); Assert.Equal(79, firmado.Value.Length);
        Assert.True(service.Validar(firmado.Value, out var validado)); Assert.Equal(id, validado);
        Assert.False(new HmacTicketTokenService(RandomNumberGenerator.GetBytes(32)).Validar(firmado.Value, out _));
        Array.Fill<byte>(key, 0);
        Assert.True(service.Validar(firmado.Value, out _)); // El servicio copia la clave.
    }

    [Fact]
    public void RechazaAlteracionDeIdentificadorFirmaVersionYFormato()
    {
        var service = new HmacTicketTokenService(RandomNumberGenerator.GetBytes(32)); var token = service.Firmar(Guid.NewGuid()).Value;
        Assert.False(service.Validar("v2" + token[2..], out _));
        Assert.False(service.Validar("v1." + Guid.NewGuid().ToString("N") + token[35..], out _));
        Assert.False(service.Validar(token[..40] + (token[40] == 'A' ? "B" : "A") + token[41..], out _));
        Assert.False(service.Validar(token + "=", out _));
        Assert.False(service.Validar(new string('x', 10000), out _));
        Assert.False(service.Validar(null, out _));
        Assert.False(service.Validar("v1." + new string('0', 32) + token[35..], out _));
    }

    [Fact]
    public void AusenciaDeClaveFallaCerradoYConfiguracionDebilSeRechaza()
    {
        var service = new HmacTicketTokenService(null);
        Assert.True(service.Firmar(Guid.NewGuid()).IsFailure); Assert.False(service.Validar("cualquiera", out _));
        Assert.Throws<ArgumentException>(() => new HmacTicketTokenService(new byte[31]));
        Assert.Throws<ArgumentException>(() => new ServiceCollection().AddEmisionBoletosBase64("formato-inválido"));
        Assert.Throws<ArgumentException>(() => new ServiceCollection().AddEmisionBoletosBase64(Convert.ToBase64String(new byte[16])));
        Assert.True(new HmacTicketTokenService(RandomNumberGenerator.GetBytes(32)).Firmar(Guid.Empty).IsFailure);
    }
}
