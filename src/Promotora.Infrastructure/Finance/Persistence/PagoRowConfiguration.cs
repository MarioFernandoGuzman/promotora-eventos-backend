using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Promotora.Infrastructure.Finance.Persistence;

internal sealed class PagoRowConfiguration : IEntityTypeConfiguration<PagoRow>
{
    public void Configure(EntityTypeBuilder<PagoRow> builder)
    {
        builder.ToTable("pagos", "public");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("bigint").IsRequired(true).UseIdentityAlwaysColumn();
        builder.Property(x => x.CompraId).HasColumnType("integer").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.Pasarela).HasColumnType("character varying(30)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.Metodo).HasColumnType("character varying(30)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.Referencia).HasColumnType("character varying(100)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.Monto).HasColumnType("numeric(10,2)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.Estado).HasColumnType("character varying(20)").IsRequired(true).HasDefaultValueSql("'PENDIENTE'::character varying");
        builder.Property(x => x.Respuesta).HasColumnType("jsonb").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.Fecha).HasColumnType("timestamp with time zone").IsRequired(true).HasDefaultValueSql("now()");
        builder.HasOne<Promotora.Infrastructure.Ticketing.Persistence.CompraRow>().WithMany().HasForeignKey(x => x.CompraId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_pagos_compra");
    }
}
