using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Promotora.Infrastructure.Reporting.Persistence;

internal sealed class UsoAgenteIaViewConfiguration : IEntityTypeConfiguration<UsoAgenteIaView>
{
    public void Configure(EntityTypeBuilder<UsoAgenteIaView> builder)
    {
        builder.HasNoKey().ToView("vw_uso_agente_ia", "bi");
        builder.Property(x => x.Fecha).HasColumnType("date").IsRequired(false);
        builder.Property(x => x.Modelo).HasColumnType("character varying").IsRequired(false);
        builder.Property(x => x.Estado).HasColumnType("character varying(20)").IsRequired(false);
        builder.Property(x => x.Consultas).HasColumnType("bigint").IsRequired(false);
        builder.Property(x => x.TiempoPromedioMs).HasColumnType("numeric").IsRequired(false);
        builder.Property(x => x.TokensEntrada).HasColumnType("bigint").IsRequired(false);
        builder.Property(x => x.TokensSalida).HasColumnType("bigint").IsRequired(false);
    }
}
