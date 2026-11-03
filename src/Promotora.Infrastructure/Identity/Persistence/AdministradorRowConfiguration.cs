using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Promotora.Infrastructure.Identity.Persistence;

internal sealed class AdministradorRowConfiguration : IEntityTypeConfiguration<AdministradorRow>
{
    public void Configure(EntityTypeBuilder<AdministradorRow> builder)
    {
        builder.ToTable("administradores", "public");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("integer").IsRequired(true).UseIdentityAlwaysColumn();
        builder.Property(x => x.UsuarioId).HasColumnType("integer").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.NivelAcceso).HasColumnType("character varying(20)").IsRequired(true).HasDefaultValueSql("'ESTANDAR'::character varying");
        builder.HasOne<UsuarioRow>().WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_administradores_usuario");
    }
}
