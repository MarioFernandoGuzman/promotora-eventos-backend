using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Promotora.Infrastructure.Finance.Persistence;

internal sealed class PagoSimuladoRowConfiguration : IEntityTypeConfiguration<PagoSimuladoRow>
{
    public void Configure(EntityTypeBuilder<PagoSimuladoRow> builder)
    {
        builder.ToTable("pagos_simulados", "public");
        builder.HasKey(x => x.Referencia);
        builder.Property(x => x.Referencia).HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(x => x.Monto).HasColumnType("numeric(10,2)").IsRequired();
        builder.Property(x => x.Moneda).HasColumnType("character varying(3)").IsRequired();
        builder.Property(x => x.Estado).HasColumnType("character varying(20)").IsRequired();
        builder.Property(x => x.Fecha).HasColumnType("timestamp with time zone").HasDefaultValueSql("now()");
    }
}
