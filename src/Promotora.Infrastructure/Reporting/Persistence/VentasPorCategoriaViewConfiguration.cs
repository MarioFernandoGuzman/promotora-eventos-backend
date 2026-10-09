using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Promotora.Infrastructure.Reporting.Persistence;

internal sealed class VentasPorCategoriaViewConfiguration : IEntityTypeConfiguration<VentasPorCategoriaView>
{
    public void Configure(EntityTypeBuilder<VentasPorCategoriaView> builder)
    {
        builder.HasNoKey().ToView("vw_ventas_por_categoria", "bi");
        builder.Property(x => x.Fecha).HasColumnType("date").IsRequired(false);
        builder.Property(x => x.EventoId).HasColumnType("integer").IsRequired(false);
        builder.Property(x => x.EventoNombre).HasColumnType("character varying(150)").IsRequired(false);
        builder.Property(x => x.EventoEstado).HasColumnType("character varying(20)").IsRequired(false);
        builder.Property(x => x.CategoriaId).HasColumnType("integer").IsRequired(false);
        builder.Property(x => x.RecintoId).HasColumnType("integer").IsRequired(false);
        builder.Property(x => x.DuenoId).HasColumnType("integer").IsRequired(false);
        builder.Property(x => x.CompraEstado).HasColumnType("character varying(20)").IsRequired(false);
        builder.Property(x => x.Moneda).HasColumnType("character(3)").IsRequired(false);
        builder.Property(x => x.ComprasCompletadas).HasColumnType("bigint").IsRequired(false);
        builder.Property(x => x.BoletosVendidos).HasColumnType("numeric").IsRequired(false);
        builder.Property(x => x.Subtotal).HasColumnType("numeric").IsRequired(false);
        builder.Property(x => x.Impuestos).HasColumnType("numeric").IsRequired(false);
        builder.Property(x => x.ComisionPlataforma).HasColumnType("numeric").IsRequired(false);
        builder.Property(x => x.Total).HasColumnType("numeric").IsRequired(false);
        builder.Property(x => x.CategoriaNombre).HasColumnType("character varying(50)").IsRequired(false);
    }
}
