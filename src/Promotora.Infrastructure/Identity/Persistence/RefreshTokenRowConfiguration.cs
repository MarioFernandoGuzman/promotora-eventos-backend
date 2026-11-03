using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Promotora.Infrastructure.Identity.Persistence;

internal sealed class RefreshTokenRowConfiguration : IEntityTypeConfiguration<RefreshTokenRow>
{
    public void Configure(EntityTypeBuilder<RefreshTokenRow> builder)
    {
        builder.ToTable("refresh_tokens", "public");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("bigint").IsRequired(true).UseIdentityAlwaysColumn();
        builder.Property(x => x.UsuarioId).HasColumnType("integer").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.TokenHash).HasColumnType("character varying(255)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.FamiliaId).HasColumnType("uuid").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.ExpiraEn).HasColumnType("timestamp with time zone").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.RevocadoEn).HasColumnType("timestamp with time zone").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.ReemplazadoPorId).HasColumnType("bigint").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.IpOrigen).HasColumnType("character varying(45)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.FechaCreacion).HasColumnType("timestamp with time zone").IsRequired(true).HasDefaultValueSql("now()");
        builder.HasOne<UsuarioRow>().WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_refresh_tokens_usuario");
        builder.HasOne<RefreshTokenRow>().WithMany().HasForeignKey(x => x.ReemplazadoPorId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_refresh_tokens_reemplazo");
    }
}
