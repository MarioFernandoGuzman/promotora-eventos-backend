using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Promotora.Infrastructure.Ticketing.Persistence;

internal sealed class DistribucionEventoRowConfiguration : IEntityTypeConfiguration<DistribucionEventoRow>
{
    public void Configure(EntityTypeBuilder<DistribucionEventoRow> builder)
    {
        builder.ToTable("distribuciones_evento", "public");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("bigint").IsRequired(true).UseIdentityAlwaysColumn();
        builder.Property(x => x.EventoId).HasColumnType("integer").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.LocalidadId).HasColumnType("integer").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.Estado).HasColumnType("character varying(20)").IsRequired(true).HasDefaultValueSql("'BORRADOR'::character varying");
        builder.Property(x => x.ImagenPlanoUrl).HasColumnType("character varying(255)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.FechaCreacion).HasColumnType("timestamp with time zone").IsRequired(true).HasDefaultValueSql("now()");
        builder.Property(x => x.FechaPublicacion).HasColumnType("timestamp with time zone").IsRequired(false).ValueGeneratedNever();
        builder.HasOne<Promotora.Infrastructure.Catalog.Persistence.EventoRow>().WithMany().HasForeignKey(x => x.EventoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Promotora.Infrastructure.Venues.Persistence.LocalidadRow>().WithMany().HasForeignKey(x => x.LocalidadId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.EventoId, x.LocalidadId }).IsUnique();
    }
}
