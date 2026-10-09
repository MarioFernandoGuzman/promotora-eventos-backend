using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Promotora.Infrastructure.Reporting.Persistence;

internal sealed class AuditoriaOperacionesViewConfiguration : IEntityTypeConfiguration<AuditoriaOperacionesView>
{
    public void Configure(EntityTypeBuilder<AuditoriaOperacionesView> builder)
    {
        builder.HasNoKey().ToView("vw_auditoria_operaciones", "bi");
        builder.Property(x => x.Fecha).HasColumnType("date").IsRequired(false);
        builder.Property(x => x.EntidadAfectada).HasColumnType("character varying(50)").IsRequired(false);
        builder.Property(x => x.Accion).HasColumnType("character varying(20)").IsRequired(false);
        builder.Property(x => x.Operaciones).HasColumnType("bigint").IsRequired(false);
        builder.Property(x => x.UsuariosIdentificados).HasColumnType("bigint").IsRequired(false);
    }
}
