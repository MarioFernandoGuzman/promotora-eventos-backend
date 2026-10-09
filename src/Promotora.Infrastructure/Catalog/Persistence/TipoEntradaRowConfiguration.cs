using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Promotora.Infrastructure.Catalog.Persistence;

internal sealed class TipoEntradaRowConfiguration : IEntityTypeConfiguration<TipoEntradaRow>
{
    public void Configure(EntityTypeBuilder<TipoEntradaRow> builder)
    {
        builder.ToTable("tipos_entrada", "public");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("integer").IsRequired(true).UseIdentityAlwaysColumn();
        builder.Property(x => x.EventoId).HasColumnType("integer").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.LocalidadId).HasColumnType("integer").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.Nombre).HasColumnType("character varying(100)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.Precio).HasColumnType("numeric(10,2)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.CupoTotal).HasColumnType("integer").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.CupoDisponible).HasColumnType("integer").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.Activo).HasColumnType("boolean").IsRequired(true).HasDefaultValueSql("true");
        builder.HasOne<EventoRow>().WithMany().HasForeignKey(x => x.EventoId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_tipos_entrada_evento");
        builder.HasOne<Promotora.Infrastructure.Venues.Persistence.LocalidadRow>().WithMany().HasForeignKey(x => x.LocalidadId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_tipos_entrada_localidad");
    }
}
