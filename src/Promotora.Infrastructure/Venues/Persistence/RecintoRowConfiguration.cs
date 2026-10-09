using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Promotora.Infrastructure.Venues.Persistence;

internal sealed class RecintoRowConfiguration : IEntityTypeConfiguration<RecintoRow>
{
    public void Configure(EntityTypeBuilder<RecintoRow> builder)
    {
        builder.ToTable("recintos", "public");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("integer").IsRequired(true).UseIdentityAlwaysColumn();
        builder.Property(x => x.DuenoId).HasColumnType("integer").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.Nombre).HasColumnType("character varying(150)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.Direccion).HasColumnType("character varying(255)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.Ciudad).HasColumnType("character varying(100)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.Latitud).HasColumnType("numeric(9,6)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.Longitud).HasColumnType("numeric(9,6)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.ImagenPlanoUrl).HasColumnType("character varying(255)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.CapacidadTotal).HasColumnType("integer").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.Activo).HasColumnType("boolean").IsRequired(true).HasDefaultValueSql("true");
        builder.Property(x => x.FechaCreacion).HasColumnType("timestamp with time zone").IsRequired(true).HasDefaultValueSql("now()");
        builder.HasOne<Promotora.Infrastructure.Identity.Persistence.DuenoRecintoRow>().WithMany().HasForeignKey(x => x.DuenoId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_recintos_dueno");
    }
}
