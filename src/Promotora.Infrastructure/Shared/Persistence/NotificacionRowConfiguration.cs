using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Promotora.Infrastructure.Shared.Persistence;

internal sealed class NotificacionRowConfiguration : IEntityTypeConfiguration<NotificacionRow>
{
    public void Configure(EntityTypeBuilder<NotificacionRow> builder)
    {
        builder.ToTable("notificaciones", "public");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("bigint").IsRequired(true).UseIdentityAlwaysColumn();
        builder.Property(x => x.UsuarioId).HasColumnType("integer").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.Tipo).HasColumnType("character varying(50)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.Estado).HasColumnType("character varying(20)").IsRequired(true).HasDefaultValueSql("'PENDIENTE'::character varying");
        builder.Property(x => x.Mensaje).HasColumnType("text").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.FechaCreacion).HasColumnType("timestamp with time zone").IsRequired(true).HasDefaultValueSql("now()");
        builder.Property(x => x.FechaEnvio).HasColumnType("timestamp with time zone").IsRequired(false).ValueGeneratedNever();
        builder.HasOne<Promotora.Infrastructure.Identity.Persistence.UsuarioRow>().WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_notificaciones_usuario");
    }
}
