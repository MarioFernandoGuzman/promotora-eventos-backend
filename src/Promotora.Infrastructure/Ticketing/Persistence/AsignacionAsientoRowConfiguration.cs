using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Promotora.Infrastructure.Ticketing.Persistence;

internal sealed class AsignacionAsientoRowConfiguration : IEntityTypeConfiguration<AsignacionAsientoRow>
{
    public void Configure(EntityTypeBuilder<AsignacionAsientoRow> builder)
    {
        builder.ToTable("asignaciones_asiento", "public");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("bigint").IsRequired(true).UseIdentityAlwaysColumn();
        builder.Property(x => x.AsientoId).HasColumnType("bigint").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.DetalleCompraId).HasColumnType("integer").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.OrdinalBoleto).HasColumnType("integer").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.Estado).HasColumnType("character varying(20)").IsRequired(true).HasDefaultValueSql("'RESERVADA'::character varying");
        builder.Property(x => x.BoletoId).HasColumnType("bigint").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.FechaReserva).HasColumnType("timestamp with time zone").IsRequired(true).HasDefaultValueSql("now()");
        builder.Property(x => x.FechaConfirmacion).HasColumnType("timestamp with time zone").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.FechaLiberacion).HasColumnType("timestamp with time zone").IsRequired(false).ValueGeneratedNever();
        builder.HasOne<AsientoEventoRow>().WithMany().HasForeignKey(x => x.AsientoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DetalleCompraRow>().WithMany().HasForeignKey(x => x.DetalleCompraId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<BoletoRow>().WithMany().HasForeignKey(x => x.BoletoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.BoletoId).IsUnique();
        builder.HasIndex(x => new { x.DetalleCompraId, x.OrdinalBoleto }).IsUnique();
        builder.HasIndex(x => x.AsientoId).IsUnique().HasFilter("estado IN ('RESERVADA','CONFIRMADA')");
    }
}
