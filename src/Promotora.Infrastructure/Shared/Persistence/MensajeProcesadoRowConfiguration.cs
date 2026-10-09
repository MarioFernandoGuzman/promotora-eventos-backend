using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Promotora.Infrastructure.Shared.Persistence;

internal sealed class MensajeProcesadoRowConfiguration : IEntityTypeConfiguration<MensajeProcesadoRow>
{
    public void Configure(EntityTypeBuilder<MensajeProcesadoRow> builder)
    {
        builder.ToTable("mensajes_procesados", "public");
        builder.HasKey(x => new { x.MensajeId, x.Consumidor });
        builder.Property(x => x.MensajeId).HasColumnType("bigint").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.Consumidor).HasColumnType("character varying(100)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.Fecha).HasColumnType("timestamp with time zone").IsRequired(true).HasDefaultValueSql("now()");
    }
}
