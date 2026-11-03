using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Promotora.Application.Ticketing;
using Promotora.Domain.Identity;
using Promotora.Infrastructure.Tests;

namespace Promotora.Api.Tests;

public sealed class CompraConcurrenciaApiTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Theory]
    [InlineData(40, 7, 1)]
    [InlineData(40, 8, 2)]
    [InlineData(33, 9, 3)]
    public async Task ClientesSimultaneosReservanExactamenteElCupoSinComprasParciales(int solicitudes, int cupo, int cantidad)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.CreateContext(); var datos = await CompraFixture.CrearAsync(db, cupo, ct);
        await using var factory = new IdentityApiFactory(fixture.ConnectionString);
        var clientes = Enumerable.Range(0, 8).Select(_ => factory.CreateClient()).ToArray();
        try
        {
            var support = new TicketingApiTestSupport(fixture);
            // Actores distintos ejercitan el cupo compartido, sin serializar todo bajo un mismo usuario.
            await Task.WhenAll(clientes.Select(client => support.LoginAsync(client, RolUsuario.CLIENTE, ct)));
            var salida = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var tareas = Enumerable.Range(0, solicitudes).Select(async i =>
            {
                await salida.Task.WaitAsync(ct);
                using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/compras")
                { Content = JsonContent.Create(new { eventoId = datos.EventoId, items = new[] { new ItemCompra(datos.TipoId, cantidad) } }) };
                string key = Guid.NewGuid().ToString(); request.Headers.Add("Idempotency-Key", key);
                using var response = await clientes[i % clientes.Length].SendAsync(request, ct);
                var compra = response.StatusCode == HttpStatusCode.Accepted ? await response.Content.ReadFromJsonAsync<CompraCreadaDto>(ct) : null;
                return (response.StatusCode, Compra: compra, Key: key);
            }).ToArray();
            salida.SetResult(); var resultados = await Task.WhenAll(tareas);
            int ganadoras = cupo / cantidad;
            Assert.Equal(ganadoras, resultados.Count(r => r.StatusCode == HttpStatusCode.Accepted));
            Assert.Equal(solicitudes - ganadoras, resultados.Count(r => r.StatusCode == HttpStatusCode.Conflict));
            Assert.All(resultados.Where(r => r.Compra is not null), r =>
            {
                Assert.Equal("PENDIENTE", r.Compra!.Estado); Assert.Equal(cantidad, Assert.Single(r.Compra.Items).Cantidad);
                Assert.Equal(100m * cantidad, r.Compra.Subtotal); Assert.Equal(r.Compra.Total, r.Compra.Subtotal + r.Compra.ComisionPlataforma + r.Compra.Impuestos);
            });
            var compras = await db.Compras.AsNoTracking().Where(c => c.EventoId == datos.EventoId).ToArrayAsync(ct);
            Assert.Equal(ganadoras, compras.Length); Assert.Equal(ganadoras, compras.Select(c => c.PublicId).Distinct().Count());
            int[] ids = compras.Select(c => c.Id).ToArray();
            string[] publicIds = compras.Select(c => c.PublicId.ToString("D")).ToArray();
            Assert.Equal(ganadoras, await db.OutboxMensajes.CountAsync(m => publicIds.Contains(m.AgregadoId) && m.Tipo == "compra.procesar", ct));
            Assert.Equal(cupo, await db.DetalleCompra.Where(d => ids.Contains(d.CompraId)).SumAsync(d => d.Cantidad, ct));
            Assert.Equal(0, await db.TiposEntrada.Where(t => t.Id == datos.TipoId).Select(t => t.CupoDisponible).SingleAsync(ct));
            var clavesFallidas = resultados.Where(r => r.StatusCode == HttpStatusCode.Conflict).Select(r => r.Key).ToArray();
            Assert.False(await db.Compras.AnyAsync(c => clavesFallidas.Contains(c.IdempotencyKey), ct));
            string[] auditoriasIds = ids.Select(id => id.ToString()).ToArray();
            Assert.Equal(ganadoras, await db.LogsAuditoria.CountAsync(a => a.EntidadAfectada == "compras" && auditoriasIds.Contains(a.EntidadId!) && a.Accion == "CREATE", ct));
            Assert.False(await db.Pagos.AnyAsync(p => ids.Contains(p.CompraId), ct));
            Assert.False(await db.MovimientosFinancieros.AnyAsync(m => m.CompraId != null && ids.Contains(m.CompraId.Value), ct));
        }
        finally { foreach (var cliente in clientes) cliente.Dispose(); }
    }
}
