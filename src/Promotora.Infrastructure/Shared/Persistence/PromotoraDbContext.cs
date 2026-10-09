using Microsoft.EntityFrameworkCore;
using Promotora.Infrastructure.AI.Persistence;
using Promotora.Infrastructure.Audit.Persistence;
using Promotora.Infrastructure.Catalog.Persistence;
using Promotora.Infrastructure.Finance.Persistence;
using Promotora.Infrastructure.Identity.Persistence;
using Promotora.Infrastructure.Reporting.Persistence;
using Promotora.Infrastructure.Reviews.Persistence;
using Promotora.Infrastructure.Shared.Persistence;
using Promotora.Infrastructure.Ticketing.Persistence;
using Promotora.Infrastructure.Venues.Persistence;

namespace Promotora.Infrastructure.Shared.Persistence;

public sealed class PromotoraDbContext(DbContextOptions<PromotoraDbContext> options) : DbContext(options)
{
    public DbSet<AsistenciaEventoView> VwAsistenciaEvento => Set<AsistenciaEventoView>();
    public DbSet<AuditoriaOperacionesView> VwAuditoriaOperaciones => Set<AuditoriaOperacionesView>();
    public DbSet<CalificacionPresentadorView> VwCalificacionPresentador => Set<CalificacionPresentadorView>();
    public DbSet<ComisionesPlataformaView> VwComisionesPlataforma => Set<ComisionesPlataformaView>();
    public DbSet<ComprasNoExitosasView> VwComprasNoExitosas => Set<ComprasNoExitosasView>();
    public DbSet<EventosPorEstadoView> VwEventosPorEstado => Set<EventosPorEstadoView>();
    public DbSet<IngresosDuenoView> VwIngresosDueno => Set<IngresosDuenoView>();
    public DbSet<InscritosCarrerasView> VwInscritosCarreras => Set<InscritosCarrerasView>();
    public DbSet<NotificacionesEstadoView> VwNotificacionesEstado => Set<NotificacionesEstadoView>();
    public DbSet<OcupacionLocalidadView> VwOcupacionLocalidad => Set<OcupacionLocalidadView>();
    public DbSet<TopClientesView> VwTopClientes => Set<TopClientesView>();
    public DbSet<UsoAgenteIaView> VwUsoAgenteIa => Set<UsoAgenteIaView>();
    public DbSet<VentasDiariasView> VwVentasDiarias => Set<VentasDiariasView>();
    public DbSet<VentasPorCategoriaView> VwVentasPorCategoria => Set<VentasPorCategoriaView>();
    public DbSet<VentasPorEventoView> VwVentasPorEvento => Set<VentasPorEventoView>();
    public DbSet<VentasPorRecintoView> VwVentasPorRecinto => Set<VentasPorRecintoView>();
    public DbSet<AdministradorRow> Administradores => Set<AdministradorRow>();
    public DbSet<BoletoRow> Boletos => Set<BoletoRow>();
    public DbSet<CategoriaEventoRow> CategoriasEvento => Set<CategoriaEventoRow>();
    public DbSet<ChunkRagRow> ChunksRag => Set<ChunkRagRow>();
    public DbSet<ClienteRow> Clientes => Set<ClienteRow>();
    public DbSet<CompraRow> Compras => Set<CompraRow>();
    public DbSet<CuentaFinancieraRow> CuentasFinancieras => Set<CuentaFinancieraRow>();
    public DbSet<DetalleCompraRow> DetalleCompra => Set<DetalleCompraRow>();
    public DbSet<DocumentoRagRow> DocumentosRag => Set<DocumentoRagRow>();
    public DbSet<DuenoRecintoRow> DuenosRecinto => Set<DuenoRecintoRow>();
    public DbSet<EventoPresentadorRow> EventoPresentadores => Set<EventoPresentadorRow>();
    public DbSet<EventoRow> Eventos => Set<EventoRow>();
    public DbSet<LocalidadRow> Localidades => Set<LocalidadRow>();
    public DbSet<LogAgenteIaRow> LogsAgenteIa => Set<LogAgenteIaRow>();
    public DbSet<LogAuditoriaRow> LogsAuditoria => Set<LogAuditoriaRow>();
    public DbSet<MensajeProcesadoRow> MensajesProcesados => Set<MensajeProcesadoRow>();
    public DbSet<MovimientoFinancieroRow> MovimientosFinancieros => Set<MovimientoFinancieroRow>();
    public DbSet<NotificacionRow> Notificaciones => Set<NotificacionRow>();
    public DbSet<OutboxMensajeRow> OutboxMensajes => Set<OutboxMensajeRow>();
    public DbSet<PagoRow> Pagos => Set<PagoRow>();
    public DbSet<PresentadorRow> Presentadores => Set<PresentadorRow>();
    public DbSet<RecintoRow> Recintos => Set<RecintoRow>();
    public DbSet<RefreshTokenRow> RefreshTokens => Set<RefreshTokenRow>();
    public DbSet<ResenaRow> Resenas => Set<ResenaRow>();
    public DbSet<TipoEntradaRow> TiposEntrada => Set<TipoEntradaRow>();
    public DbSet<UsuarioRow> Usuarios => Set<UsuarioRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PromotoraDbContext).Assembly);
    }
}
