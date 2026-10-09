using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Promotora.Infrastructure.Reporting.Persistence;

internal sealed class ComisionesPlataformaViewConfiguration : IEntityTypeConfiguration<ComisionesPlataformaView>
{
    public void Configure(EntityTypeBuilder<ComisionesPlataformaView> builder)
    {
        builder.HasNoKey().ToView("vw_comisiones_plataforma", "bi");
        builder.Property(x => x.Fecha).HasColumnType("date").IsRequired(false);
        builder.Property(x => x.Moneda).HasColumnType("character(3)").IsRequired(false);
        builder.Property(x => x.EventoId).HasColumnType("integer").IsRequired(false);
        builder.Property(x => x.CategoriaId).HasColumnType("integer").IsRequired(false);
        builder.Property(x => x.RecintoId).HasColumnType("integer").IsRequired(false);
        builder.Property(x => x.DuenoId).HasColumnType("integer").IsRequired(false);
        builder.Property(x => x.EventoEstado).HasColumnType("character varying(20)").IsRequired(false);
        builder.Property(x => x.CompraEstado).HasColumnType("character varying(20)").IsRequired(false);
        builder.Property(x => x.Comisiones).HasColumnType("numeric").IsRequired(false);
        builder.Property(x => x.Reembolsos).HasColumnType("numeric").IsRequired(false);
        builder.Property(x => x.ComisionNeta).HasColumnType("numeric").IsRequired(false);
    }
}
