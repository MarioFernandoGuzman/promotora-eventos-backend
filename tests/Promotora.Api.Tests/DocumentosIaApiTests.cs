using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Promotora.Application.AI.Documentos;
using Promotora.Application.Audit;
using Promotora.Application.Shared;
using Promotora.Domain.Identity;
using Promotora.Infrastructure.Tests;

namespace Promotora.Api.Tests;

public sealed class DocumentosIaApiTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    [Fact]
    public async Task CargaDetalleVersionesYDesactivacionConservanHistoriaAuditada()
    {
        await using var factory = new IdentityApiFactory(postgres.ConnectionString); using var admin = factory.CreateClient();
        int usuario = await new TicketingApiTestSupport(postgres).LoginAsync(admin, RolUsuario.ADMINISTRADOR, Ct);
        var creada = await CrearAsync(admin); Assert.Equal(1, creada.Version); Assert.Equal("PENDIENTE", creada.Estado); Assert.Equal(usuario, creada.SubidoPor); Assert.Equal(DateTimeKind.Utc, creada.FechaCarga.Kind);
        using var contenido = Formulario("Reglamento segunda versión");
        using var version = await admin.PostAsync($"/api/v1/ia/documentos/{creada.Id}/versiones", contenido, Ct);
        Assert.Equal(HttpStatusCode.Created, version.StatusCode);
        var segunda = (await version.Content.ReadFromJsonAsync<DocumentoDto>(Ct))!; Assert.Equal(2, segunda.Version); Assert.NotEqual(creada.Id, segunda.Id);
        var detalle = (await admin.GetFromJsonAsync<DocumentoDetalleDto>($"/api/v1/ia/documentos/{creada.Id}", Ct))!;
        Assert.Equal("Reglamento de acceso público", detalle.Contenido); Assert.Equal(new[] { 2, 1 }, detalle.Versiones.Select(v => v.Version));
        using var viejo = Formulario(); using var conflicto = await admin.PostAsync($"/api/v1/ia/documentos/{creada.Id}/versiones", viejo, Ct); Assert.Equal(HttpStatusCode.Conflict, conflicto.StatusCode);
        using var invalido = await admin.PostAsJsonAsync($"/api/v1/ia/documentos/{creada.Id}/desactivar", new { estadoEsperado = "INDEXADO" }, Ct); Assert.Equal(HttpStatusCode.Conflict, invalido.StatusCode);
        using var desactivada = await admin.PostAsJsonAsync($"/api/v1/ia/documentos/{creada.Id}/desactivar", new { estadoEsperado = "PENDIENTE" }, Ct); Assert.Equal(HttpStatusCode.OK, desactivada.StatusCode);
        using var repetida = await admin.PostAsJsonAsync($"/api/v1/ia/documentos/{creada.Id}/desactivar", new { estadoEsperado = "PENDIENTE" }, Ct); Assert.Equal(HttpStatusCode.OK, repetida.StatusCode);
        var pagina = (await admin.GetFromJsonAsync<PaginaDto<DocumentoDto>>("/api/v1/ia/documentos?estado=INACTIVO&page=1&pageSize=1", Ct))!;
        Assert.Equal(1, pagina.PageSize); Assert.Contains(pagina.Items, d => d.Id == creada.Id);
        await using var db = postgres.CreateContext();
        Assert.Equal("Reglamento de acceso público", (await db.DocumentosRag.SingleAsync(d => d.Id == creada.Id, Ct)).Contenido);
        Assert.Equal("PENDIENTE", (await db.DocumentosRag.SingleAsync(d => d.Id == segunda.Id, Ct)).Estado);
        Assert.Equal(1, await db.LogsAuditoria.CountAsync(a => a.EntidadAfectada == "documentos_rag" && a.EntidadId == creada.Id.ToString() && a.Accion == "INGEST", Ct));
        Assert.Equal(1, await db.LogsAuditoria.CountAsync(a => a.EntidadAfectada == "documentos_rag" && a.EntidadId == creada.Id.ToString() && a.Accion == "UPDATE", Ct));
        Assert.False(await db.ChunksRag.AnyAsync(c => c.DocumentoId == creada.Id || c.DocumentoId == segunda.Id, Ct));
    }
    [Fact]
    public async Task VersionesConcurrentesConAdministradoresDistintosSoloCreanUnaSiguiente()
    {
        await using var factory = new IdentityApiFactory(postgres.ConnectionString); using var admin = factory.CreateClient(); using var otro = factory.CreateClient();
        var soporte = new TicketingApiTestSupport(postgres); await soporte.LoginAsync(admin, RolUsuario.ADMINISTRADOR, Ct); await soporte.LoginAsync(otro, RolUsuario.ADMINISTRADOR, Ct);
        var original = await CrearAsync(admin);
        using var uno = Formulario("Versión A"); using var dos = Formulario("Versión B");
        var respuestas = await Task.WhenAll(admin.PostAsync($"/api/v1/ia/documentos/{original.Id}/versiones", uno, Ct), otro.PostAsync($"/api/v1/ia/documentos/{original.Id}/versiones", dos, Ct));
        try { Assert.Single(respuestas, r => r.StatusCode == HttpStatusCode.Created); Assert.Single(respuestas, r => r.StatusCode == HttpStatusCode.Conflict); }
        finally { foreach (var respuesta in respuestas) respuesta.Dispose(); }
        var detalle = (await admin.GetFromJsonAsync<DocumentoDetalleDto>($"/api/v1/ia/documentos/{original.Id}", Ct))!;
        Assert.Equal(new[] { 2, 1 }, detalle.Versiones.Select(v => v.Version));
    }
    [Fact]
    public async Task PermisosSeAplicanATodasLasRutasSinLecturaPublica()
    {
        await using var factory = new IdentityApiFactory(postgres.ConnectionString);
        foreach (RolUsuario? rol in new RolUsuario?[] { null, RolUsuario.CLIENTE, RolUsuario.DUENO_RECINTO })
        {
            using var cliente = factory.CreateClient(); if (rol.HasValue) await new TicketingApiTestSupport(postgres).LoginAsync(cliente, rol.Value, Ct);
            var esperado = rol is null ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden;
            using var form = Formulario(); using var formVersion = Formulario();
            using var crear = await cliente.PostAsync("/api/v1/ia/documentos", form, Ct);
            using var lista = await cliente.GetAsync("/api/v1/ia/documentos", Ct);
            using var detalle = await cliente.GetAsync("/api/v1/ia/documentos/1", Ct);
            using var version = await cliente.PostAsync("/api/v1/ia/documentos/1/versiones", formVersion, Ct);
            using var desactivar = await cliente.PostAsJsonAsync("/api/v1/ia/documentos/1/desactivar", new { estadoEsperado = "PENDIENTE" }, Ct);
            foreach (var respuesta in new[] { crear, lista, detalle, version, desactivar }) Assert.Equal(esperado, respuesta.StatusCode);
        }
    }
    [Fact]
    public async Task AdministradorDeshabilitadoNoIngresaNiLeeDocumentos()
    {
        await using var factory = new IdentityApiFactory(postgres.ConnectionString); using var admin = factory.CreateClient();
        int usuario = await new TicketingApiTestSupport(postgres).LoginAsync(admin, RolUsuario.ADMINISTRADOR, Ct);
        await using var db = postgres.CreateContext(); await db.Usuarios.Where(u => u.Id == usuario).ExecuteUpdateAsync(s => s.SetProperty(u => u.Estado, "INACTIVO"), Ct);
        using var form = Formulario(); using var crear = await admin.PostAsync("/api/v1/ia/documentos", form, Ct);
        using var lista = await admin.GetAsync("/api/v1/ia/documentos", Ct);
        Assert.Contains(crear.StatusCode, new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden }); Assert.Equal(crear.StatusCode, lista.StatusCode);
    }
    [Fact]
    public async Task ArchivosInvalidosYFiltrosNoDejanFilasNiAuditoriaDeIngesta()
    {
        await using var factory = new IdentityApiFactory(postgres.ConnectionString); using var admin = factory.CreateClient();
        int usuario = await new TicketingApiTestSupport(postgres).LoginAsync(admin, RolUsuario.ADMINISTRADOR, Ct);
        foreach (string nombre in new[] { "archivo.exe", "falso.pdf", "falso.docx" })
        {
            using var form = Formulario("contenido", nombre); using var respuesta = await admin.PostAsync("/api/v1/ia/documentos", form, Ct); Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        }
        using var sinArchivo = new MultipartFormDataContent(); sinArchivo.Add(new StringContent("Guía"), "titulo");
        using var faltante = await admin.PostAsync("/api/v1/ia/documentos", sinArchivo, Ct); Assert.Equal(HttpStatusCode.BadRequest, faltante.StatusCode);
        using var grande = new MultipartFormDataContent(); grande.Add(new StringContent("Guía"), "titulo"); grande.Add(new ByteArrayContent(new byte[DocumentoLimites.MaxBytes + 1]), "archivo", "grande.txt");
        using var limite = await admin.PostAsync("/api/v1/ia/documentos", grande, Ct); Assert.Equal(HttpStatusCode.BadRequest, limite.StatusCode);
        foreach (string query in new[] { "page=0", "pageSize=101", "estado=INVALIDO" })
        { using var respuesta = await admin.GetAsync($"/api/v1/ia/documentos?{query}", Ct); Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode); }
        using var inexistente = await admin.GetAsync($"/api/v1/ia/documentos/{int.MaxValue}", Ct); Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);
        await using var db = postgres.CreateContext(); Assert.False(await db.DocumentosRag.AnyAsync(d => d.SubidoPor == usuario, Ct));
        Assert.False(await db.LogsAuditoria.AnyAsync(a => a.UsuarioId == usuario && a.Accion == "INGEST", Ct));
    }
    [Fact]
    public async Task FalloDeAuditoriaRevierteElDocumentoYSwaggerPublicaElFormulario()
    {
        await using var factory = new IdentityApiFactory(postgres.ConnectionString); using var admin = factory.CreateClient();
        var spec = await admin.GetStringAsync("/swagger/v1/swagger.json", Ct);
        Assert.Contains("/api/v1/ia/documentos/{id}/versiones", spec); Assert.Contains("multipart/form-data", spec);
        int usuario = await new TicketingApiTestSupport(postgres).LoginAsync(admin, RolUsuario.ADMINISTRADOR, Ct);
        using var servicio = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddScoped<IAuditLogger, AuditoriaFallida>()));
        using var cliente = servicio.CreateClient(); cliente.DefaultRequestHeaders.Authorization = admin.DefaultRequestHeaders.Authorization;
        using var form = Formulario(); using var respuesta = await cliente.PostAsync("/api/v1/ia/documentos", form, Ct); Assert.Equal(HttpStatusCode.InternalServerError, respuesta.StatusCode);
        await using var db = postgres.CreateContext(); Assert.False(await db.DocumentosRag.AnyAsync(d => d.SubidoPor == usuario, Ct));
    }
    private static async Task<DocumentoDto> CrearAsync(HttpClient admin)
    {
        using var form = Formulario(); using var respuesta = await admin.PostAsync("/api/v1/ia/documentos", form, Ct);
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode); Assert.NotNull(respuesta.Headers.Location);
        return (await respuesta.Content.ReadFromJsonAsync<DocumentoDto>(Ct))!;
    }
    private static MultipartFormDataContent Formulario(string texto = "Reglamento de acceso público", string nombre = "reglamento.md")
    {
        var form = new MultipartFormDataContent(); form.Add(new StringContent("Reglamento"), "titulo"); form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(texto)), "archivo", nombre); return form;
    }
    private sealed class AuditoriaFallida : IAuditLogger
    {
        public Task WriteAsync(int? usuarioId, string accion, string entidad, string? entidadId, string? ip, CancellationToken ct) => throw new InvalidOperationException("Fallo simulado.");
        public Task WriteAsync(int? usuarioId, string accion, string entidad, string? entidadId, string? ip, DetalleAuditoria? detalle, CancellationToken ct) => throw new InvalidOperationException("Fallo simulado.");
    }
}
