using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Promotora.Application.Catalog;
using Promotora.Application.Shared.Images;
using Promotora.Domain.Identity;
using Promotora.Infrastructure.Tests;
using Promotora.WebApi.Catalog;

namespace Promotora.Api.Tests;

public sealed partial class CatalogApiTests
{
    private static async Task<HttpResponseMessage> UploadAsync(HttpClient client, string url, byte[] bytes, CancellationToken ct, string mime = "image/png", string name = "../../foto.png")
    {
        using var multipart = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes); file.Headers.ContentType = new MediaTypeHeaderValue(mime);
        multipart.Add(file, "archivo", name);
        return await client.PostAsync(url, multipart, ct);
    }
    private static async Task<ImagenSubidaDto> ImagenAsync(HttpClient client, string url, CancellationToken ct)
    {
        using var response = await UploadAsync(client, url, ImagenFixture.Imagen(), ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ImagenSubidaDto>(ct))!;
    }

    [Fact]
    public async Task ImagenesValidanPropiedadPersistenUrlYSePublicanSoloConRecursoVisible()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new IdentityApiFactory(fixture.ConnectionString);
        using var owner = factory.CreateClient(); using var admin = factory.CreateClient(); using var other = factory.CreateClient(); using var publico = factory.CreateClient();
        await LoginAsync(owner, await CreateUserAsync(RolUsuario.DUENO_RECINTO, ct), ct);
        await LoginAsync(other, await CreateUserAsync(RolUsuario.DUENO_RECINTO, ct), ct);
        await LoginAsync(admin, await CreateUserAsync(RolUsuario.ADMINISTRADOR, ct), ct);
        int recinto = await CrearRecintoAsync(owner, ct);
        var evento = await PostAsync<EventoDto>(owner, "/api/v1/eventos", DatosEvento(recinto), ct);
        var presentador = await PostAsync<PresentadorDto>(admin, "/api/v1/presentadores", new { nombre = "Imagen", tipo = "ARTISTA" }, ct);
        var plano = await ImagenAsync(owner, $"/api/v1/recintos/{recinto}/imagen-plano", ct);
        var fotoEvento = await ImagenAsync(owner, $"/api/v1/eventos/{evento.Id}/imagen", ct);
        var fotoPresentador = await ImagenAsync(admin, $"/api/v1/presentadores/{presentador.Id}/imagen", ct);
        await ProblemAsync(await UploadAsync(other, $"/api/v1/recintos/{recinto}/imagen-plano", ImagenFixture.Imagen(), ct), 403, ct);
        await ProblemAsync(await UploadAsync(other, $"/api/v1/eventos/{evento.Id}/imagen", ImagenFixture.Imagen(), ct), 403, ct);
        await ProblemAsync(await UploadAsync(owner, $"/api/v1/presentadores/{presentador.Id}/imagen", ImagenFixture.Imagen(), ct), 403, ct);
        await ProblemAsync(await UploadAsync(publico, $"/api/v1/recintos/{recinto}/imagen-plano", ImagenFixture.Imagen(), ct), 401, ct);
        Assert.Equal(3, Directory.GetFiles(factory.ImagesRoot).Length);
        await ProblemAsync(await publico.GetAsync(fotoEvento.Url, ct), 404, ct);
        await ProblemAsync(await publico.GetAsync(fotoPresentador.Url, ct), 404, ct);
        using var png = await publico.GetAsync(plano.Url, ct);
        Assert.Equal(HttpStatusCode.OK, png.StatusCode);
        Assert.Equal("image/png", png.Content.Headers.ContentType!.MediaType);
        Assert.Equal("nosniff", Assert.Single(png.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("no-store", png.Headers.CacheControl!.ToString());
        await PostAsync<EventoPresentadorDto>(owner, $"/api/v1/eventos/{evento.Id}/presentadores", new { presentadorId = presentador.Id }, ct);
        Assert.Equal(HttpStatusCode.OK, (await owner.PutAsJsonAsync($"/api/v1/eventos/{evento.Id}/estado", new EstadoEventoRequest("BORRADOR", "PROGRAMADO"), ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await publico.GetAsync(fotoEvento.Url, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await publico.GetAsync(fotoPresentador.Url, ct)).StatusCode);
        var detail = (await publico.GetFromJsonAsync<EventoPublicoDto>($"/api/v1/catalogo/eventos/{evento.Id}", ct))!;
        Assert.Equal(fotoEvento.Url, detail.ImagenUrl); Assert.Equal(fotoPresentador.Url, Assert.Single(detail.Presentadores).ImagenUrl);
        var replacement = await ImagenAsync(owner, $"/api/v1/recintos/{recinto}/imagen-plano", ct);
        Assert.NotEqual(plano.Url, replacement.Url);
        await ProblemAsync(await publico.GetAsync(plano.Url, ct), 404, ct);
        Assert.Equal(HttpStatusCode.OK, (await admin.DeleteAsync($"/api/v1/presentadores/{presentador.Id}", ct)).StatusCode);
        await ProblemAsync(await publico.GetAsync(fotoPresentador.Url, ct), 404, ct);
        Assert.Equal(HttpStatusCode.OK, (await owner.DeleteAsync($"/api/v1/eventos/{evento.Id}", ct)).StatusCode);
        await ProblemAsync(await publico.GetAsync(fotoEvento.Url, ct), 404, ct);
        await using var db = fixture.CreateContext();
        Assert.Equal(replacement.Url, (await db.Recintos.AsNoTracking().SingleAsync(x => x.Id == recinto, ct)).ImagenPlanoUrl);
        Assert.Equal(2, await db.LogsAuditoria.CountAsync(x => x.EntidadAfectada == "recintos" && x.EntidadId == recinto.ToString() && x.Accion == "UPDATE", ct));
        var schema = await publico.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json", ct);
        var formSchema = schema.GetProperty("paths").GetProperty("/api/v1/recintos/{id}/imagen-plano").GetProperty("post")
            .GetProperty("requestBody").GetProperty("content").GetProperty("multipart/form-data").GetProperty("schema");
        if (formSchema.TryGetProperty("$ref", out var reference))
            formSchema = schema.GetProperty("components").GetProperty("schemas").GetProperty(reference.GetString()!.Split('/')[^1]);
        Assert.True(formSchema.TryGetProperty("properties", out var properties), formSchema.ToString());
        Assert.True(properties.TryGetProperty("archivo", out var archivo), properties.ToString());
        Assert.Equal("string", archivo.GetProperty("type").GetString());
        Assert.Equal("binary", archivo.GetProperty("format").GetString());
        Assert.Equal("binary", schema.GetProperty("paths").GetProperty("/api/v1/imagenes/{nombre}").GetProperty("get")
            .GetProperty("responses").GetProperty("200").GetProperty("content").GetProperty("image/png").GetProperty("schema").GetProperty("format").GetString());
    }

    [Fact]
    public async Task SubidasInvalidasNoCreanArchivosNiAuditoria()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new IdentityApiFactory(fixture.ConnectionString);
        using var owner = factory.CreateClient(); using var customer = factory.CreateClient();
        await LoginAsync(owner, await CreateUserAsync(RolUsuario.DUENO_RECINTO, ct), ct);
        await LoginAsync(customer, await CreateUserAsync(RolUsuario.CLIENTE, ct), ct);
        int recinto = await CrearRecintoAsync(owner, ct);
        string url = $"/api/v1/recintos/{recinto}/imagen-plano";
        await ProblemAsync(await UploadAsync(customer, url, ImagenFixture.Imagen(), ct), 403, ct);
        await ProblemAsync(await UploadAsync(owner, url, Encoding.UTF8.GetBytes("<svg/>"), ct, "image/svg+xml"), 400, ct);
        await ProblemAsync(await UploadAsync(owner, url, Encoding.UTF8.GetBytes("<svg/>"), ct), 400, ct);
        await ProblemAsync(await UploadAsync(owner, url, ImagenFixture.Imagen(), ct, "image/jpeg"), 400, ct);
        await ProblemAsync(await UploadAsync(owner, url, new byte[ImagenLimites.MaxArchivoBytes + 1], ct), 400, ct);
        using var empty = new MultipartFormDataContent();
        await ProblemAsync(await owner.PostAsync(url, empty, ct), 400, ct);
        await ProblemAsync(await UploadAsync(owner, "/api/v1/recintos/2147483647/imagen-plano", ImagenFixture.Imagen(), ct), 404, ct);
        Assert.Equal(HttpStatusCode.OK, (await owner.DeleteAsync($"/api/v1/recintos/{recinto}", ct)).StatusCode);
        await ProblemAsync(await UploadAsync(owner, url, ImagenFixture.Imagen(), ct), 409, ct);
        Assert.False(Directory.Exists(factory.ImagesRoot));
        await using var db = fixture.CreateContext();
        Assert.Equal(0, await db.LogsAuditoria.CountAsync(x => x.EntidadAfectada == "recintos" && x.EntidadId == recinto.ToString() && x.Accion == "UPDATE", ct));
    }
}
