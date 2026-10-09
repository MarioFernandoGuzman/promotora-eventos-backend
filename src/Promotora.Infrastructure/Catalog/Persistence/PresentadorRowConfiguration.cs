using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Promotora.Infrastructure.Catalog.Persistence;

internal sealed class PresentadorRowConfiguration : IEntityTypeConfiguration<PresentadorRow>
{
    public void Configure(EntityTypeBuilder<PresentadorRow> builder)
    {
        builder.ToTable("presentadores", "public");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("integer").IsRequired(true).UseIdentityAlwaysColumn();
        builder.Property(x => x.Nombre).HasColumnType("character varying(150)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.Tipo).HasColumnType("character varying(20)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.Biografia).HasColumnType("text").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.ImagenUrl).HasColumnType("character varying(255)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.Activo).HasColumnType("boolean").IsRequired(true).HasDefaultValueSql("true").HasSentinel(true);
    }
}
