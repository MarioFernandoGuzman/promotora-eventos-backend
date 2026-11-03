using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Promotora.Infrastructure.AI.Persistence;

internal sealed class DocumentoRagRowConfiguration : IEntityTypeConfiguration<DocumentoRagRow>
{
    public void Configure(EntityTypeBuilder<DocumentoRagRow> builder)
    {
        builder.ToTable("documentos_rag", "public");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("integer").IsRequired(true).UseIdentityAlwaysColumn();
        builder.Property(x => x.Titulo).HasColumnType("character varying(200)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.TipoDocumento).HasColumnType("character varying(50)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.Contenido).HasColumnType("text").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.RutaArchivo).HasColumnType("character varying(255)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.Version).HasColumnType("integer").IsRequired(true).HasDefaultValueSql("1").HasSentinel(1);
        builder.Property(x => x.FechaCarga).HasColumnType("timestamp with time zone").IsRequired(true).HasDefaultValueSql("now()");
        builder.Property(x => x.Estado).HasColumnType("character varying(20)").IsRequired(true).HasDefaultValueSql("'PENDIENTE'::character varying");
        builder.Property(x => x.SubidoPor).HasColumnType("integer").IsRequired(true).ValueGeneratedNever();
        builder.HasOne<Promotora.Infrastructure.Identity.Persistence.UsuarioRow>().WithMany().HasForeignKey(x => x.SubidoPor).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_documentos_rag_usuario");
    }
}
