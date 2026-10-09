using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Promotora.Infrastructure.Identity.Persistence;

internal sealed class ClienteRowConfiguration : IEntityTypeConfiguration<ClienteRow>
{
    public void Configure(EntityTypeBuilder<ClienteRow> builder)
    {
        builder.ToTable("clientes", "public");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("integer").IsRequired(true).UseIdentityAlwaysColumn();
        builder.Property(x => x.UsuarioId).HasColumnType("integer").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.FechaNacimiento).HasColumnType("date").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.Telefono).HasColumnType("character varying(20)").IsRequired(false).ValueGeneratedNever();
        builder.HasOne<UsuarioRow>().WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_clientes_usuario");
    }
}
