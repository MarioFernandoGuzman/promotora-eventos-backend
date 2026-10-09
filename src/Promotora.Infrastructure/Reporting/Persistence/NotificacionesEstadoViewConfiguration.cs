using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Promotora.Infrastructure.Reporting.Persistence;

internal sealed class NotificacionesEstadoViewConfiguration : IEntityTypeConfiguration<NotificacionesEstadoView>
{
    public void Configure(EntityTypeBuilder<NotificacionesEstadoView> builder)
    {
        builder.HasNoKey().ToView("vw_notificaciones_estado", "bi");
        builder.Property(x => x.Fecha).HasColumnType("date").IsRequired(false);
        builder.Property(x => x.Tipo).HasColumnType("character varying(50)").IsRequired(false);
        builder.Property(x => x.Estado).HasColumnType("character varying(20)").IsRequired(false);
        builder.Property(x => x.Notificaciones).HasColumnType("bigint").IsRequired(false);
    }
}
