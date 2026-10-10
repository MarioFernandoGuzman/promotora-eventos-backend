using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Promotora.Infrastructure.Shared.Persistence;

internal sealed class OutboxMensajeRowConfiguration : IEntityTypeConfiguration<OutboxMensajeRow>
{
    public void Configure(EntityTypeBuilder<OutboxMensajeRow> builder)
    {
        builder.ToTable("outbox_mensajes", "public");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("bigint").IsRequired(true).UseIdentityAlwaysColumn();
        builder.Property(x => x.Agregado).HasColumnType("character varying").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.AgregadoId).HasColumnType("character varying").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.Tipo).HasColumnType("character varying").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.Payload).HasColumnType("jsonb").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.Publicado).HasColumnType("boolean").IsRequired(true).HasDefaultValueSql("false").HasSentinel(false);
        builder.Property(x => x.Fecha).HasColumnType("timestamp with time zone").IsRequired(true).HasDefaultValueSql("now()");
        builder.Property(x => x.FechaPublicado).HasColumnType("timestamp with time zone").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.Intentos).HasColumnType("integer").IsRequired(true).HasDefaultValueSql("0");
    }
}
