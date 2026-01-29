using Microsoft.EntityFrameworkCore;
using SmartArchivist.Dal.Entities;
using SmartArchivist.Contract.Enums;

namespace SmartArchivist.Dal.Data
{
    /// <summary>
    /// Represents the Entity Framework Core database context, providing access to document entities and
    /// configuring the database schema.
    /// </summary>
    public class SmartArchivistDbContext : DbContext
    {
        public SmartArchivistDbContext(DbContextOptions<SmartArchivistDbContext> options)
            : base(options) { }

        public DbSet<DocumentEntity> Documents => Set<DocumentEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.HasDefaultSchema("smartarchivist");

            modelBuilder.Entity<DocumentEntity>(b =>
            {
                b.ToTable("documents");

                b.HasKey(x => x.Id);
                b.Property(x => x.Name).HasMaxLength(255).IsRequired();
                b.HasIndex(x => x.Name).IsUnique();
                b.Property(x => x.FilePath).HasMaxLength(2048).IsRequired();
                b.Property(x => x.FileExtension).HasMaxLength(32).IsRequired();
                b.Property(x => x.ContentType).HasMaxLength(128).IsRequired();
                b.Property(x => x.UploadDate).IsRequired();
                b.Property(x => x.FileSize).IsRequired();
                b.Property(x => x.State).IsRequired().HasDefaultValue(DocumentState.Uploaded);
                b.Property(x => x.OcrText).IsRequired(false);
                b.Property(x => x.GenAiSummary).IsRequired(false);
                b.Property(x => x.Tags).HasColumnType("text[]").IsRequired(false);
            });
        }
    }
}