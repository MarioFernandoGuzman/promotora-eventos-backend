using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Promotora.Application.Identity.Registration;
using Promotora.Application.Identity.Sessions;
using Promotora.Application.Ticketing;
using Promotora.Domain.Identity;
using Promotora.Infrastructure.Audit;
using Promotora.Infrastructure.Identity;
using Promotora.Infrastructure.Identity.Persistence;
using Promotora.Infrastructure.Shared;
using Promotora.Infrastructure.Shared.Persistence;
using Promotora.Infrastructure.Tests;
using Promotora.Infrastructure.Ticketing;
using Promotora.Infrastructure.Ticketing.Persistence;

namespace Promotora.Api.Tests;

internal sealed class TicketingApiTestSupport(PostgresFixture fixture)
{
    public async Task<int> LoginAsync(HttpClient client, RolUsuario rol, CancellationToken ct)
    {
        await using var db = fixture.CreateContext(); var clock = new SystemClock(TimeProvider.System);
        string email = $"{Guid.NewGuid():N}@example.test", password = Guid.NewGuid().ToString("N");
        var repo = new EfUsuarioRepository(db, new EfUnitOfWork(db), new EfAuditLogger(db, clock));
        var usuario = await repo.CreateAsync(new Usuario(0, email, new IdentityPasswordService().Hash(password), "Prueba", "Ticketing", rol, "ACTIVO", false, clock.UtcNow),
            new PerfilNuevo(Empresa: rol == RolUsuario.DUENO_RECINTO ? "Prueba" : null), null, null, false, ct);
        using var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password }, ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await response.Content.ReadFromJsonAsync<TokenDto>(ct))!.AccessToken);
        return usuario.Value.Id;
    }
    public static async Task<CompraCreadaDto> ComprarAsync(HttpClient client, int evento, int tipo, int cantidad, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/compras")
        { Content = JsonContent.Create(new { eventoId = evento, items = new[] { new ItemCompra(tipo, cantidad) } }) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var response = await client.SendAsync(request, ct); Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CompraCreadaDto>(ct))!;
    }
    // Compra gratuita existente en PostgreSQL temporal; total cero no requiere movimientos.
    public static async Task<BoletoRow> BoletoGratisAsync(PromotoraDbContext db, Guid publicId, byte[] key, string estado, CancellationToken ct)
    {
        var compra = await db.Compras.AsNoTracking().SingleAsync(c => c.PublicId == publicId, ct); Assert.Equal(0m, compra.Total);
        await db.Compras.Where(c => c.Id == compra.Id && c.Estado == "PENDIENTE").ExecuteUpdateAsync(s => s.SetProperty(c => c.Estado, "COMPLETADA"), ct);
        var id = Guid.NewGuid();
        var boleto = new BoletoRow
        {
            PublicId = id,
            DetalleCompraId = await db.DetalleCompra.Where(d => d.CompraId == compra.Id).Select(d => d.Id).SingleAsync(ct),
            CodigoQr = new HmacTicketTokenService(key).Firmar(id).Value,
            Estado = estado,
            NombreAsistente = "Asistente de prueba",
            FechaEmision = DateTime.UtcNow
        };
        db.Boletos.Add(boleto); await db.SaveChangesAsync(ct); return boleto;
    }
}
