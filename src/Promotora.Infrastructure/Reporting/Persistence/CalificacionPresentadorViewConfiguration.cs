using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Promotora.Infrastructure.Reporting.Persistence;

internal sealed class CalificacionPresentadorViewConfiguration : IEntityTypeConfiguration<CalificacionPresentadorView>
{
    public void Configure(EntityTypeBuilder<CalificacionPresentadorView> builder)
    {
        builder.HasNoKey().ToView("vw_calificacion_presentador", "bi");
        builder.Property(x => x.Fecha).HasColumnType("date").IsRequired(false);
        builder.Property(x => x.PresentadorId).HasColumnType("integer").IsRequired(false);
        builder.Property(x => x.PresentadorNombre).HasColumnType("character varying(150)").IsRequired(false);
        builder.Property(x => x.EventoId).HasColumnType("integer").IsRequired(false);
        builder.Property(x => x.CategoriaId).HasColumnType("integer").IsRequired(false);
        builder.Property(x => x.RecintoId).HasColumnType("integer").IsRequired(false);
        builder.Property(x => x.DuenoId).HasColumnType("integer").IsRequired(false);
        builder.Property(x => x.EventoEstado).HasColumnType("character varying(20)").IsRequired(false);
        builder.Property(x => x.ResenasAprobadas).HasColumnType("bigint").IsRequired(false);
        builder.Property(x => x.SumaCalificaciones).HasColumnType("bigint").IsRequired(false);
        builder.Property(x => x.CalificacionPromedio).HasColumnType("numeric").IsRequired(false);
    }
}
