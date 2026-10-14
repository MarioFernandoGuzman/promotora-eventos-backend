using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Promotora.Application.AI.Documentos;
using Promotora.Application.Shared;
using Promotora.Infrastructure.Shared.Persistence;

namespace Promotora.Infrastructure.AI.Persistence;

public sealed class EfDocumentoRepository(PromotoraDbContext db) : IDocumentoRepository
{
    private static readonly Expression<Func<DocumentoRagRow, DocumentoDto>> Proyeccion = d =>
        new(d.Id, d.Titulo, d.TipoDocumento, d.Version, d.Estado, d.FechaCarga, d.SubidoPor);

    public async Task<PaginaDto<DocumentoDto>> ListarAsync(DocumentosQuery request, CancellationToken ct)
    {
        var query = db.DocumentosRag.AsNoTracking();
        if (request.Estado is not null) query = query.Where(d => d.Estado == request.Estado);
        int total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(d => d.FechaCarga).ThenByDescending(d => d.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).Select(Proyeccion).ToArrayAsync(ct);
        return new(items, request.Page, request.PageSize, total);
    }
    public async Task<DocumentoDetalleDto?> DetalleAsync(int id, CancellationToken ct)
    {
        var documento = await db.DocumentosRag.AsNoTracking().SingleOrDefaultAsync(d => d.Id == id, ct);
        if (documento is null) return null;
        var versiones = await db.DocumentosRag.AsNoTracking()
            .Where(d => documento.RutaArchivo != null ? d.RutaArchivo == documento.RutaArchivo : d.Id == id)
            .OrderByDescending(d => d.Version).ThenByDescending(d => d.Id).Select(Proyeccion).ToArrayAsync(ct);
        return new(Mapear(documento), documento.Contenido, versiones);
    }
    public async Task<DocumentoGuardado?> BloquearSerieAsync(int id, CancellationToken ct)
    {
        ExigirTransaccion();
        var documento = await db.DocumentosRag.AsNoTracking().SingleOrDefaultAsync(d => d.Id == id, ct);
        if (documento is null) return null;
        // Todas las versiones bloquean la misma primera fila. Bajo READ COMMITTED, la consulta
        // posterior observa la versión que pudo crear quien tenía el bloqueo anteriormente.
        int raiz = documento.RutaArchivo is null ? id : await db.DocumentosRag
            .Where(d => d.RutaArchivo == documento.RutaArchivo).MinAsync(d => d.Id, ct);
        await db.DocumentosRag.FromSqlInterpolated($"SELECT * FROM documentos_rag WHERE id={raiz} FOR UPDATE").AsNoTracking().ToArrayAsync(ct);
        documento = await db.DocumentosRag.AsNoTracking().SingleAsync(d => d.Id == id, ct);
        return new(Mapear(documento), documento.RutaArchivo);
    }
    public Task<int> UltimaVersionAsync(string referencia, CancellationToken ct) => db.DocumentosRag
        .Where(d => d.RutaArchivo == referencia).MaxAsync(d => d.Version, ct);

    public async Task<DocumentoDto> CrearAsync(string titulo, TextoExtraido texto, string referencia, int version, int usuario, DateTime fecha, CancellationToken ct)
    {
        ExigirTransaccion();
        if (fecha.Kind != DateTimeKind.Utc) throw new InvalidOperationException("La fecha debe ser UTC.");
        var row = new DocumentoRagRow
        {
            Titulo = titulo,
            TipoDocumento = texto.TipoDocumento,
            Contenido = texto.Contenido,
            RutaArchivo = referencia,
            Version = version,
            SubidoPor = usuario,
            FechaCarga = fecha,
            Estado = "PENDIENTE"
        };
        db.DocumentosRag.Add(row);
        await db.SaveChangesAsync(ct);
        return Mapear(row);
    }
    public async Task<bool> DesactivarAsync(int id, string esperado, CancellationToken ct)
    {
        ExigirTransaccion();
        return await db.DocumentosRag.Where(d => d.Id == id && d.Estado == esperado && d.Estado != "INACTIVO")
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.Estado, "INACTIVO"), ct) == 1;
    }
    private void ExigirTransaccion()
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Gestionar documentos requiere transacción.");
    }
    private static DocumentoDto Mapear(DocumentoRagRow d) => new(d.Id, d.Titulo, d.TipoDocumento, d.Version, d.Estado, d.FechaCarga, d.SubidoPor);
}
