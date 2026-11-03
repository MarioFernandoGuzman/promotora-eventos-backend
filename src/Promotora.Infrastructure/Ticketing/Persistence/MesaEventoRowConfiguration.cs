using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Promotora.Infrastructure.Ticketing.Persistence;

internal sealed class MesaEventoRowConfiguration : IEntityTypeConfiguration<MesaEventoRow>
{
    public void Configure(EntityTypeBuilder<MesaEventoRow> builder)
    {
        builder.ToTable("mesas_evento", "public");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("bigint").IsRequired(true).UseIdentityAlwaysColumn();
        builder.Property(x => x.DistribucionId).HasColumnType("bigint").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.Etiqueta).HasColumnType("character varying(50)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.Modalidad).HasColumnType("character varying(20)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.TipoEntradaId).HasColumnType("integer").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.X).HasColumnType("numeric(10,6)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.Y).HasColumnType("numeric(10,6)").IsRequired(true).ValueGeneratedNever();
        builder.HasOne<DistribucionEventoRow>().WithMany().HasForeignKey(x => x.DistribucionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Promotora.Infrastructure.Catalog.Persistence.TipoEntradaRow>().WithMany().HasForeignKey(x => x.TipoEntradaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasAlternateKey(x => new { x.Id, x.DistribucionId, x.TipoEntradaId });
        builder.HasIndex(x => new { x.DistribucionId, x.Etiqueta }).IsUnique();
    }
}
