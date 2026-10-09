using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Promotora.Infrastructure.Audit.Persistence;

internal sealed class LogAuditoriaRowConfiguration : IEntityTypeConfiguration<LogAuditoriaRow>
{
    public void Configure(EntityTypeBuilder<LogAuditoriaRow> builder)
    {
        builder.ToTable("logs_auditoria", "public");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("bigint").IsRequired(true).UseIdentityAlwaysColumn();
        builder.Property(x => x.UsuarioId).HasColumnType("integer").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.EntidadAfectada).HasColumnType("character varying(50)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.EntidadId).HasColumnType("character varying(50)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.Accion).HasColumnType("character varying(20)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.Detalle).HasColumnType("jsonb").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.IpOrigen).HasColumnType("character varying(45)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.FechaHora).HasColumnType("timestamp with time zone").IsRequired(true).HasDefaultValueSql("now()");
        builder.HasOne<Promotora.Infrastructure.Identity.Persistence.UsuarioRow>().WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_logs_auditoria_usuario");
    }
}
