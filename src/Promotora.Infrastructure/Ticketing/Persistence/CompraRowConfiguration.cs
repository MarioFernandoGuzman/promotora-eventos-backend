using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Promotora.Infrastructure.Ticketing.Persistence;

internal sealed class CompraRowConfiguration : IEntityTypeConfiguration<CompraRow>
{
    public void Configure(EntityTypeBuilder<CompraRow> builder)
    {
        builder.ToTable("compras", "public");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("integer").IsRequired(true).UseIdentityAlwaysColumn();
        builder.Property(x => x.PublicId).HasColumnType("uuid").IsRequired(true).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.ClienteId).HasColumnType("integer").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.EventoId).HasColumnType("integer").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.FechaCompra).HasColumnType("timestamp with time zone").IsRequired(true).HasDefaultValueSql("now()");
        builder.Property(x => x.Estado).HasColumnType("character varying(20)").IsRequired(true).HasDefaultValueSql("'PENDIENTE'::character varying");
        builder.Property(x => x.ExpiraEn).HasColumnType("timestamp with time zone").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.ProcesandoDesde).HasColumnType("timestamp with time zone").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.IdempotencyKey).HasColumnType("character varying(100)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.Moneda).HasColumnType("character(3)").IsRequired(true).HasDefaultValueSql("'GTQ'::bpchar");
        builder.Property(x => x.Subtotal).HasColumnType("numeric(10,2)").IsRequired(true).HasDefaultValueSql("0");
        builder.Property(x => x.Impuestos).HasColumnType("numeric(10,2)").IsRequired(true).HasDefaultValueSql("0");
        builder.Property(x => x.ComisionPlataforma).HasColumnType("numeric(10,2)").IsRequired(true).HasDefaultValueSql("0");
        builder.Property(x => x.Total).HasColumnType("numeric(10,2)").IsRequired(true).HasDefaultValueSql("0");
        builder.HasOne<Promotora.Infrastructure.Identity.Persistence.ClienteRow>().WithMany().HasForeignKey(x => x.ClienteId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_compras_cliente");
        builder.HasOne<Promotora.Infrastructure.Catalog.Persistence.EventoRow>().WithMany().HasForeignKey(x => x.EventoId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_compras_evento");
    }
}
