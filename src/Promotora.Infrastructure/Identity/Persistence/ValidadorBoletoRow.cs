using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Promotora.Infrastructure.Identity.Persistence;

internal sealed class ValidadorBoletoRow
{
    public int UsuarioId { get; init; }
}

internal sealed class ValidadorBoletoRowConfiguration : IEntityTypeConfiguration<ValidadorBoletoRow>
{
    public void Configure(EntityTypeBuilder<ValidadorBoletoRow> builder)
    {
        builder.ToTable("validadores_boletos", "public");
        builder.HasKey(x => x.UsuarioId);
        builder.Property(x => x.UsuarioId).ValueGeneratedNever();
        builder.HasOne<UsuarioRow>().WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Restrict);
    }
}
