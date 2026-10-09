using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Promotora.Infrastructure.Reviews.Persistence;

internal sealed class ResenaRowConfiguration : IEntityTypeConfiguration<ResenaRow>
{
    public void Configure(EntityTypeBuilder<ResenaRow> builder)
    {
        builder.ToTable("resenas", "public");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("integer").IsRequired(true).UseIdentityAlwaysColumn();
        builder.Property(x => x.EventoId).HasColumnType("integer").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.ClienteId).HasColumnType("integer").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.Calificacion).HasColumnType("smallint").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.Comentario).HasColumnType("text").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.Estado).HasColumnType("character varying(20)").IsRequired(true).HasDefaultValueSql("'PENDIENTE'::character varying");
        builder.Property(x => x.FechaCreacion).HasColumnType("timestamp with time zone").IsRequired(true).HasDefaultValueSql("now()");
        builder.HasOne<Promotora.Infrastructure.Catalog.Persistence.EventoRow>().WithMany().HasForeignKey(x => x.EventoId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_resenas_evento");
        builder.HasOne<Promotora.Infrastructure.Identity.Persistence.ClienteRow>().WithMany().HasForeignKey(x => x.ClienteId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_resenas_cliente");
    }
}
