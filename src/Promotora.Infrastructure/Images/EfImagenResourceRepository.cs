using Microsoft.EntityFrameworkCore;
using Promotora.Application.Shared.Images;
using Promotora.Infrastructure.Shared.Persistence;

namespace Promotora.Infrastructure.Images;

public sealed class EfImagenResourceRepository(PromotoraDbContext db) : IImagenResourceRepository
{
    public async Task<bool> AssignAsync(DestinoImagen destino, int id, string url, string? estadoEsperado, CancellationToken ct) => destino switch
    {
        DestinoImagen.Recinto => await db.Recintos.Where(x => x.Id == id && x.Activo).ExecuteUpdateAsync(s => s.SetProperty(x => x.ImagenPlanoUrl, url), ct) == 1,
        DestinoImagen.Evento => await db.Eventos.Where(x => x.Id == id && x.Estado == estadoEsperado).ExecuteUpdateAsync(s => s.SetProperty(x => x.ImagenUrl, url), ct) == 1,
        DestinoImagen.Presentador => await db.Presentadores.Where(x => x.Id == id && x.Activo).ExecuteUpdateAsync(s => s.SetProperty(x => x.ImagenUrl, url), ct) == 1,
        _ => false
    };
    public async Task<bool> PublicReferenceAsync(string url, CancellationToken ct)
    {
        if (await db.Recintos.AnyAsync(r => r.Activo && r.ImagenPlanoUrl == url, ct)) return true;
        var eventos = db.Eventos.Where(e => (e.Estado == "PROGRAMADO" || e.Estado == "EN_VENTA" || e.Estado == "AGOTADO")
            && db.Recintos.Any(r => r.Id == e.RecintoId && r.Activo) && db.CategoriasEvento.Any(c => c.Id == e.CategoriaId && c.Activo));
        if (await eventos.AnyAsync(e => e.ImagenUrl == url, ct)) return true;
        return await db.Presentadores.AnyAsync(p => p.Activo && p.ImagenUrl == url
            && db.EventoPresentadores.Any(ep => ep.PresentadorId == p.Id && eventos.Any(e => e.Id == ep.EventoId)), ct);
    }
}
