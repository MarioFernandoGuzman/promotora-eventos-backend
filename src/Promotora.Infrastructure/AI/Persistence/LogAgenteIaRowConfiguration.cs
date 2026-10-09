using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Promotora.Infrastructure.AI.Persistence;

internal sealed class LogAgenteIaRowConfiguration : IEntityTypeConfiguration<LogAgenteIaRow>
{
    public void Configure(EntityTypeBuilder<LogAgenteIaRow> builder)
    {
        builder.ToTable("logs_agente_ia", "public");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("bigint").IsRequired(true).UseIdentityAlwaysColumn();
        builder.Property(x => x.UsuarioId).HasColumnType("integer").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.Consulta).HasColumnType("text").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.Respuesta).HasColumnType("text").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.FuentesCitadas).HasColumnType("jsonb").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.TiempoRespuestaMs).HasColumnType("integer").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.FechaHora).HasColumnType("timestamp with time zone").IsRequired(true).HasDefaultValueSql("now()");
        builder.Property(x => x.SessionId).HasColumnType("uuid").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.Modelo).HasColumnType("character varying").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.TokensEntrada).HasColumnType("integer").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.TokensSalida).HasColumnType("integer").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.Estado).HasColumnType("character varying(20)").IsRequired(true).ValueGeneratedNever();
        builder.HasOne<Promotora.Infrastructure.Identity.Persistence.UsuarioRow>().WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_logs_agente_ia_usuario");
    }
}
