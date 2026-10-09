using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Promotora.Infrastructure.Reporting.Persistence;

internal sealed class AsistenciaEventoViewConfiguration : IEntityTypeConfiguration<AsistenciaEventoView>
{
    public void Configure(EntityTypeBuilder<AsistenciaEventoView> builder)
    {
        builder.HasNoKey().ToView("vw_asistencia_evento", "bi");
        builder.Property(x => x.Fecha).HasColumnType("date").IsRequired(false);
        builder.Property(x => x.EventoId).HasColumnType("integer").IsRequired(false);
        builder.Property(x => x.EventoNombre).HasColumnType("character varying(150)").IsRequired(false);
        builder.Property(x => x.EventoEstado).HasColumnType("character varying(20)").IsRequired(false);
        builder.Property(x => x.CategoriaId).HasColumnType("integer").IsRequired(false);
        builder.Property(x => x.RecintoId).HasColumnType("integer").IsRequired(false);
        builder.Property(x => x.DuenoId).HasColumnType("integer").IsRequired(false);
        builder.Property(x => x.Moneda).HasColumnType("character(3)").IsRequired(false);
        builder.Property(x => x.BoletosEmitidos).HasColumnType("bigint").IsRequired(false);
        builder.Property(x => x.BoletosVendidos).HasColumnType("bigint").IsRequired(false);
        builder.Property(x => x.BoletosUsados).HasColumnType("bigint").IsRequired(false);
        builder.Property(x => x.BoletosAnulados).HasColumnType("bigint").IsRequired(false);
    }
}
