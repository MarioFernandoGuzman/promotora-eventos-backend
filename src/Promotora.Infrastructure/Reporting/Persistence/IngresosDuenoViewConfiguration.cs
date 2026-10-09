using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Promotora.Infrastructure.Reporting.Persistence;

internal sealed class IngresosDuenoViewConfiguration : IEntityTypeConfiguration<IngresosDuenoView>
{
    public void Configure(EntityTypeBuilder<IngresosDuenoView> builder)
    {
        builder.HasNoKey().ToView("vw_ingresos_dueno", "bi");
        builder.Property(x => x.Fecha).HasColumnType("date").IsRequired(false);
        builder.Property(x => x.DuenoId).HasColumnType("integer").IsRequired(false);
        builder.Property(x => x.CuentaId).HasColumnType("integer").IsRequired(false);
        builder.Property(x => x.Moneda).HasColumnType("character(3)").IsRequired(false);
        builder.Property(x => x.EventoId).HasColumnType("integer").IsRequired(false);
        builder.Property(x => x.CategoriaId).HasColumnType("integer").IsRequired(false);
        builder.Property(x => x.RecintoId).HasColumnType("integer").IsRequired(false);
        builder.Property(x => x.EventoEstado).HasColumnType("character varying(20)").IsRequired(false);
        builder.Property(x => x.CompraEstado).HasColumnType("character varying(20)").IsRequired(false);
        builder.Property(x => x.Ventas).HasColumnType("numeric").IsRequired(false);
        builder.Property(x => x.Reembolsos).HasColumnType("numeric").IsRequired(false);
        builder.Property(x => x.Liquidaciones).HasColumnType("numeric").IsRequired(false);
        builder.Property(x => x.MovimientoNeto).HasColumnType("numeric").IsRequired(false);
    }
}
