using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Promotora.Infrastructure.Catalog.Persistence;

internal sealed class EventoPresentadorRowConfiguration : IEntityTypeConfiguration<EventoPresentadorRow>
{
    public void Configure(EntityTypeBuilder<EventoPresentadorRow> builder)
    {
        builder.ToTable("evento_presentadores", "public");
        builder.HasKey(x => new { x.EventoId, x.PresentadorId });
        builder.Property(x => x.EventoId).HasColumnType("integer").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.PresentadorId).HasColumnType("integer").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.Rol).HasColumnType("character varying(30)").IsRequired(false).ValueGeneratedNever();
        builder.HasOne<EventoRow>().WithMany().HasForeignKey(x => x.EventoId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_evento_presentadores_evento");
        builder.HasOne<PresentadorRow>().WithMany().HasForeignKey(x => x.PresentadorId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_evento_presentadores_presentador");
    }
}
