using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Promotora.Infrastructure.Reporting.Persistence;

internal sealed class ComprasNoExitosasViewConfiguration : IEntityTypeConfiguration<ComprasNoExitosasView>
{
    public void Configure(EntityTypeBuilder<ComprasNoExitosasView> builder)
    {
        builder.HasNoKey().ToView("vw_compras_no_exitosas", "bi");
        builder.Property(x => x.CompraPublicId).HasColumnType("uuid").IsRequired(false);
        builder.Property(x => x.FechaCompra).HasColumnType("timestamp with time zone").IsRequired(false);
        builder.Property(x => x.Fecha).HasColumnType("date").IsRequired(false);
        builder.Property(x => x.CompraEstado).HasColumnType("character varying(20)").IsRequired(false);
        builder.Property(x => x.Moneda).HasColumnType("character(3)").IsRequired(false);
        builder.Property(x => x.Total).HasColumnType("numeric(10,2)").IsRequired(false);
        builder.Property(x => x.ClienteId).HasColumnType("integer").IsRequired(false);
        builder.Property(x => x.EventoId).HasColumnType("integer").IsRequired(false);
        builder.Property(x => x.CategoriaId).HasColumnType("integer").IsRequired(false);
        builder.Property(x => x.RecintoId).HasColumnType("integer").IsRequired(false);
        builder.Property(x => x.DuenoId).HasColumnType("integer").IsRequired(false);
        builder.Property(x => x.EventoEstado).HasColumnType("character varying(20)").IsRequired(false);
        builder.Property(x => x.IntentosPago).HasColumnType("bigint").IsRequired(false);
        builder.Property(x => x.PagosRechazados).HasColumnType("bigint").IsRequired(false);
    }
}
