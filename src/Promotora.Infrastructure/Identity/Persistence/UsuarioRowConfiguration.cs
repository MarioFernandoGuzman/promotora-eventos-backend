using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Promotora.Infrastructure.Identity.Persistence;

internal sealed class UsuarioRowConfiguration : IEntityTypeConfiguration<UsuarioRow>
{
    public void Configure(EntityTypeBuilder<UsuarioRow> builder)
    {
        builder.ToTable("usuarios", "public");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("integer").IsRequired(true).UseIdentityAlwaysColumn();
        builder.Property(x => x.Email).HasColumnType("character varying(150)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.PasswordHash).HasColumnType("character varying(255)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.DebeCambiarPassword).HasColumnType("boolean").IsRequired(true).HasDefaultValueSql("false").HasSentinel(false);
        builder.Property(x => x.Nombre).HasColumnType("character varying(100)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.Apellido).HasColumnType("character varying(100)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.Rol).HasColumnType("character varying(20)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.Estado).HasColumnType("character varying(20)").IsRequired(true).HasDefaultValueSql("'ACTIVO'::character varying");
        builder.Property(x => x.FechaRegistro).HasColumnType("timestamp with time zone").IsRequired(true).HasDefaultValueSql("now()");
    }
}
