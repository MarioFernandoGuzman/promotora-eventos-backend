using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Promotora.Infrastructure.Reporting.Persistence;

internal sealed class TopClientesViewConfiguration : IEntityTypeConfiguration<TopClientesView>
{
    public void Configure(EntityTypeBuilder<TopClientesView> builder)
    {
        builder.HasNoKey().ToView("vw_top_clientes", "bi");
        builder.Property(x => x.Fecha).HasColumnType("date").IsRequired(false);
        builder.Property(x => x.ClienteId).HasColumnType("integer").IsRequired(false);
        builder.Property(x => x.Nombre).HasColumnType("character varying(100)").IsRequired(false);
        builder.Property(x => x.Apellido).HasColumnType("character varying(100)").IsRequired(false);
        builder.Property(x => x.EventoId).HasColumnType("integer").IsRequired(false);
        builder.Property(x => x.CategoriaId).HasColumnType("integer").IsRequired(false);
        builder.Property(x => x.RecintoId).HasColumnType("integer").IsRequired(false);
        builder.Property(x => x.DuenoId).HasColumnType("integer").IsRequired(false);
        builder.Property(x => x.EventoEstado).HasColumnType("character varying(20)").IsRequired(false);
        builder.Property(x => x.CompraEstado).HasColumnType("character varying(20)").IsRequired(false);
        builder.Property(x => x.Moneda).HasColumnType("character(3)").IsRequired(false);
        builder.Property(x => x.ComprasCompletadas).HasColumnType("bigint").IsRequired(false);
        builder.Property(x => x.Total).HasColumnType("numeric").IsRequired(false);
    }
}
