using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Promotora.Infrastructure.AI.Persistence;

internal sealed class ChunkRagRowConfiguration : IEntityTypeConfiguration<ChunkRagRow>
{
    public void Configure(EntityTypeBuilder<ChunkRagRow> builder)
    {
        builder.ToTable("chunks_rag", "public");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("bigint").IsRequired(true).UseIdentityAlwaysColumn();
        builder.Property(x => x.DocumentoId).HasColumnType("integer").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.Orden).HasColumnType("integer").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.Pagina).HasColumnType("integer").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.Contenido).HasColumnType("text").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.Embedding).HasColumnType("vector(1536)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.Metadata).HasColumnType("jsonb").IsRequired(false).ValueGeneratedNever();
        builder.HasOne<DocumentoRagRow>().WithMany().HasForeignKey(x => x.DocumentoId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_chunks_rag_documento");
    }
}
