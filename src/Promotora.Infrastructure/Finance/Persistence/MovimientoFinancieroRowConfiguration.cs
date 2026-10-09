using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Promotora.Infrastructure.Finance.Persistence;

internal sealed class MovimientoFinancieroRowConfiguration : IEntityTypeConfiguration<MovimientoFinancieroRow>
{
    public void Configure(EntityTypeBuilder<MovimientoFinancieroRow> builder)
    {
        builder.ToTable("movimientos_financieros", "public");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("bigint").IsRequired(true).UseIdentityAlwaysColumn();
        builder.Property(x => x.CuentaId).HasColumnType("integer").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.CompraId).HasColumnType("integer").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.Tipo).HasColumnType("character varying(20)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.Monto).HasColumnType("numeric(12,2)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.Descripcion).HasColumnType("text").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.Fecha).HasColumnType("timestamp with time zone").IsRequired(true).HasDefaultValueSql("now()");
        builder.HasOne<CuentaFinancieraRow>().WithMany().HasForeignKey(x => x.CuentaId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_movimientos_financieros_cuenta");
        builder.HasOne<Promotora.Infrastructure.Ticketing.Persistence.CompraRow>().WithMany().HasForeignKey(x => x.CompraId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_movimientos_financieros_compra");
    }
}
