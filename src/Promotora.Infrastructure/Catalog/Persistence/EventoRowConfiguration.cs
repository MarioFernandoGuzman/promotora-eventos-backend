using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Promotora.Infrastructure.Catalog.Persistence;

internal sealed class EventoRowConfiguration : IEntityTypeConfiguration<EventoRow>
{
    public void Configure(EntityTypeBuilder<EventoRow> builder)
    {
        builder.ToTable("eventos", "public");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("integer").IsRequired(true).UseIdentityAlwaysColumn();
        builder.Property(x => x.RecintoId).HasColumnType("integer").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.CategoriaId).HasColumnType("integer").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.CreadoPor).HasColumnType("integer").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.Nombre).HasColumnType("character varying(150)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.Descripcion).HasColumnType("text").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.FechaInicio).HasColumnType("timestamp with time zone").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.FechaFin).HasColumnType("timestamp with time zone").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.FechaInicioVenta).HasColumnType("timestamp with time zone").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.FechaFinVenta).HasColumnType("timestamp with time zone").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.MaxBoletosPorCompra).HasColumnType("integer").IsRequired(true).HasDefaultValueSql("10").HasSentinel(10);
        builder.Property(x => x.Estado).HasColumnType("character varying(20)").IsRequired(true).HasDefaultValueSql("'BORRADOR'::character varying");
        builder.Property(x => x.ImagenUrl).HasColumnType("character varying(255)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.FechaCreacion).HasColumnType("timestamp with time zone").IsRequired(true).HasDefaultValueSql("now()");
        builder.HasOne<Promotora.Infrastructure.Venues.Persistence.RecintoRow>().WithMany().HasForeignKey(x => x.RecintoId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_eventos_recinto");
        builder.HasOne<CategoriaEventoRow>().WithMany().HasForeignKey(x => x.CategoriaId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_eventos_categoria");
        builder.HasOne<Promotora.Infrastructure.Identity.Persistence.UsuarioRow>().WithMany().HasForeignKey(x => x.CreadoPor).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_eventos_creador");
    }
}
