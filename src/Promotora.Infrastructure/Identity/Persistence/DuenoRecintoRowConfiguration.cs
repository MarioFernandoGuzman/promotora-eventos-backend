using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Promotora.Infrastructure.Identity.Persistence;

internal sealed class DuenoRecintoRowConfiguration : IEntityTypeConfiguration<DuenoRecintoRow>
{
    public void Configure(EntityTypeBuilder<DuenoRecintoRow> builder)
    {
        builder.ToTable("duenos_recinto", "public");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("integer").IsRequired(true).UseIdentityAlwaysColumn();
        builder.Property(x => x.UsuarioId).HasColumnType("integer").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.Empresa).HasColumnType("character varying(150)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.Nit).HasColumnType("character varying(20)").IsRequired(false).ValueGeneratedNever();
        builder.HasOne<UsuarioRow>().WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_duenos_recinto_usuario");
    }
}
