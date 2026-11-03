using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Promotora.Infrastructure.Reporting.Persistence;

internal sealed class EventosPorEstadoViewConfiguration : IEntityTypeConfiguration<EventosPorEstadoView>
{
    public void Configure(EntityTypeBuilder<EventosPorEstadoView> builder)
    {
        builder.HasNoKey().ToView("vw_eventos_por_estado", "bi");
        builder.Property(x => x.EventoId).HasColumnType("integer").IsRequired(false);
        builder.Property(x => x.EventoNombre).HasColumnType("character varying(150)").IsRequired(false);
        builder.Property(x => x.EventoEstado).HasColumnType("character varying(20)").IsRequired(false);
        builder.Property(x => x.FechaInicio).HasColumnType("timestamp with time zone").IsRequired(false);
        builder.Property(x => x.Fecha).HasColumnType("date").IsRequired(false);
        builder.Property(x => x.CategoriaId).HasColumnType("integer").IsRequired(false);
        builder.Property(x => x.RecintoId).HasColumnType("integer").IsRequired(false);
        builder.Property(x => x.DuenoId).HasColumnType("integer").IsRequired(false);
    }
}
