using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Promotora.Infrastructure.Reporting.Persistence;

internal sealed class InscritosCarrerasViewConfiguration : IEntityTypeConfiguration<InscritosCarrerasView>
{
    public void Configure(EntityTypeBuilder<InscritosCarrerasView> builder)
    {
        builder.HasNoKey().ToView("vw_inscritos_carreras", "bi");
        builder.Property(x => x.Fecha).HasColumnType("date").IsRequired(false);
        builder.Property(x => x.EventoId).HasColumnType("integer").IsRequired(false);
        builder.Property(x => x.EventoNombre).HasColumnType("character varying(150)").IsRequired(false);
        builder.Property(x => x.EventoEstado).HasColumnType("character varying(20)").IsRequired(false);
        builder.Property(x => x.CategoriaId).HasColumnType("integer").IsRequired(false);
        builder.Property(x => x.RecintoId).HasColumnType("integer").IsRequired(false);
        builder.Property(x => x.DuenoId).HasColumnType("integer").IsRequired(false);
        builder.Property(x => x.Distancia).HasColumnType("text").IsRequired(false);
        builder.Property(x => x.Inscritos).HasColumnType("bigint").IsRequired(false);
        builder.Property(x => x.ParticipantesValidados).HasColumnType("bigint").IsRequired(false);
    }
}
