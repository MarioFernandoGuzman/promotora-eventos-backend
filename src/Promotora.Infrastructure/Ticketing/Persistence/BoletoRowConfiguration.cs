using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Promotora.Infrastructure.Ticketing.Persistence;

internal sealed class BoletoRowConfiguration : IEntityTypeConfiguration<BoletoRow>
{
    public void Configure(EntityTypeBuilder<BoletoRow> builder)
    {
        builder.ToTable("boletos", "public");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("bigint").IsRequired(true).UseIdentityAlwaysColumn();
        builder.Property(x => x.PublicId).HasColumnType("uuid").IsRequired(true).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.DetalleCompraId).HasColumnType("integer").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.CodigoQr).HasColumnType("character varying(100)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.Estado).HasColumnType("character varying(20)").IsRequired(true).HasDefaultValueSql("'VALIDO'::character varying");
        builder.Property(x => x.NombreAsistente).HasColumnType("character varying").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.DocumentoIdentidad).HasColumnType("character varying").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.DatosExtra).HasColumnType("jsonb").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.FechaEmision).HasColumnType("timestamp with time zone").IsRequired(true).HasDefaultValueSql("now()");
        builder.Property(x => x.FechaUso).HasColumnType("timestamp with time zone").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.ValidadoPor).HasColumnType("integer").IsRequired(false).ValueGeneratedNever();
        builder.HasOne<DetalleCompraRow>().WithMany().HasForeignKey(x => x.DetalleCompraId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_boletos_detalle_compra");
        builder.HasOne<Promotora.Infrastructure.Identity.Persistence.UsuarioRow>().WithMany().HasForeignKey(x => x.ValidadoPor).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_boletos_validador");
    }
}
