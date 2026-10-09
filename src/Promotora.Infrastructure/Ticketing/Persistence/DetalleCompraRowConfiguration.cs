using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Promotora.Infrastructure.Ticketing.Persistence;

internal sealed class DetalleCompraRowConfiguration : IEntityTypeConfiguration<DetalleCompraRow>
{
    public void Configure(EntityTypeBuilder<DetalleCompraRow> builder)
    {
        builder.ToTable("detalle_compra", "public");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("integer").IsRequired(true).UseIdentityAlwaysColumn();
        builder.Property(x => x.CompraId).HasColumnType("integer").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.TipoEntradaId).HasColumnType("integer").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.Cantidad).HasColumnType("integer").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.PrecioUnitario).HasColumnType("numeric(10,2)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.Subtotal).HasColumnType("numeric(10,2)").IsRequired(true).ValueGeneratedNever();
        builder.HasOne<CompraRow>().WithMany().HasForeignKey(x => x.CompraId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_detalle_compra_compra");
        builder.HasOne<Promotora.Infrastructure.Catalog.Persistence.TipoEntradaRow>().WithMany().HasForeignKey(x => x.TipoEntradaId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_detalle_compra_tipo_entrada");
    }
}
