using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Promotora.Infrastructure.Finance.Persistence;

internal sealed class CuentaFinancieraRowConfiguration : IEntityTypeConfiguration<CuentaFinancieraRow>
{
    public void Configure(EntityTypeBuilder<CuentaFinancieraRow> builder)
    {
        builder.ToTable("cuentas_financieras", "public");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("integer").IsRequired(true).UseIdentityAlwaysColumn();
        builder.Property(x => x.Tipo).HasColumnType("character varying(20)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.DuenoId).HasColumnType("integer").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.Moneda).HasColumnType("character(3)").IsRequired(true).HasDefaultValueSql("'GTQ'::bpchar");
        builder.Property(x => x.Saldo).HasColumnType("numeric(12,2)").IsRequired(true).HasDefaultValueSql("0");
        builder.Property(x => x.Estado).HasColumnType("character varying(20)").IsRequired(true).HasDefaultValueSql("'ACTIVA'::character varying");
        builder.Property(x => x.FechaCierre).HasColumnType("timestamp with time zone").IsRequired(false).ValueGeneratedNever();
        builder.HasOne<Promotora.Infrastructure.Identity.Persistence.DuenoRecintoRow>().WithMany().HasForeignKey(x => x.DuenoId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_cuentas_financieras_dueno");
    }
}
