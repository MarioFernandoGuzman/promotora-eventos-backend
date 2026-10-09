using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Promotora.Infrastructure.Venues.Persistence;

internal sealed class LocalidadRowConfiguration : IEntityTypeConfiguration<LocalidadRow>
{
    public void Configure(EntityTypeBuilder<LocalidadRow> builder)
    {
        builder.ToTable("localidades", "public");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("integer").IsRequired(true).UseIdentityAlwaysColumn();
        builder.Property(x => x.RecintoId).HasColumnType("integer").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.Nombre).HasColumnType("character varying(100)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.Capacidad).HasColumnType("integer").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.Geometria).HasColumnType("jsonb").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.Color).HasColumnType("character varying(7)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.Activo).HasColumnType("boolean").IsRequired(true).HasDefaultValueSql("true").HasSentinel(true);
        builder.HasOne<RecintoRow>().WithMany().HasForeignKey(x => x.RecintoId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_localidades_recinto");
    }
}
