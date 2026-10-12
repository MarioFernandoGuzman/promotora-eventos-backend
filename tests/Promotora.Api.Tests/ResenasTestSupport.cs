using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Promotora.Application.Identity.Sessions;
using Promotora.Infrastructure.Identity;
using Promotora.Infrastructure.Tests;

namespace Promotora.Api.Tests;

internal static class ResenasTestSupport
{
    public static async Task<(VentaReembolso Venta, int Usuario)> VentaAsync(PostgresFixture postgres, IdentityApiFactory factory,
        HttpClient cliente, CancellationToken ct)
    {
        await using var db = postgres.CreateContext(); var venta = await ReembolsoFixture.VentaAsync(db, postgres.ConnectionString, factory.QrKey, false, ct);
        int perfil = await db.Compras.Where(c => c.Id == venta.Id).Select(c => c.ClienteId).SingleAsync(ct);
        int usuario = await db.Clientes.Where(c => c.Id == perfil).Select(c => c.UsuarioId).SingleAsync(ct);
        string password = Guid.NewGuid().ToString("N"), hash = new IdentityPasswordService().Hash(password);
        await db.Usuarios.Where(u => u.Id == usuario).ExecuteUpdateAsync(s => s.SetProperty(u => u.PasswordHash, hash), ct);
        string email = await db.Usuarios.Where(u => u.Id == usuario).Select(u => u.Email).SingleAsync(ct);
        using var login = await cliente.PostAsJsonAsync("/api/v1/auth/login", new { email, password }, ct); Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<TokenDto>(ct))!.AccessToken);
        return (venta, usuario);
    }
    public static async Task UsarAsync(PostgresFixture postgres, HttpClient admin, VentaReembolso venta, CancellationToken ct)
    {
        await using var db = postgres.CreateContext(); string qr = await db.Boletos.Where(b => b.DetalleCompraId == venta.DetalleId).OrderBy(b => b.Id).Select(b => b.CodigoQr).FirstAsync(ct);
        using var respuesta = await admin.PostAsJsonAsync("/api/v1/boletos/validar", new { codigoQr = qr }, ct); Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }
}
