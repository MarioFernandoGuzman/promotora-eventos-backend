using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Promotora.Infrastructure.Catalog.Persistence;

internal sealed class CategoriaEventoRowConfiguration : IEntityTypeConfiguration<CategoriaEventoRow>
{
    public void Configure(EntityTypeBuilder<CategoriaEventoRow> builder)
    {
        builder.ToTable("categorias_evento", "public");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("integer").IsRequired(true).UseIdentityAlwaysColumn();
        builder.Property(x => x.Nombre).HasColumnType("character varying(50)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.Descripcion).HasColumnType("character varying(255)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.Activo).HasColumnType("boolean").IsRequired(true).HasDefaultValueSql("true").HasSentinel(true);
    }
}
