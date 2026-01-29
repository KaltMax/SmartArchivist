using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using SmartArchivist.Contract.Logger;
using SmartArchivist.Dal.Data;
using SmartArchivist.Dal.Entities;
using SmartArchivist.Dal.Repositories;

namespace Tests.SmartArchivist.DalTests
{
    public class DocumentRepositoryTests
    {
        private static SmartArchivistDbContext CreateContext(SqliteConnection connection)
        {
            var options = new DbContextOptionsBuilder<SmartArchivistDbContext>()
                .UseSqlite(connection)
                .Options;
            var ctx = new SmartArchivistDbContext(options);
            ctx.Database.EnsureCreated();
            return ctx;
        }

        private static DocumentEntity NewDoc(Guid? id = null, string? name = null, DateTime? uploaded = null) =>
            new()
            {
                Id = id ?? Guid.NewGuid(),
                Name = name ?? "test.pdf",
                FilePath = "/tmp/test.pdf",
                FileExtension = ".pdf",
                UploadDate = uploaded ?? DateTime.UtcNow,
                FileSize = 12345,
                OcrText = null,
                GenAiSummary = null
            };

        [Fact]
        public async Task AddAsync_Persists_Document()
        {
            await using var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
            await using var ctx = CreateContext(connection);
            var logger = Substitute.For<ILoggerWrapper<DocumentRepository>>();
            var repo = new DocumentRepository(ctx, logger);

            var doc = NewDoc();
            var saved = await repo.AddAsync(doc);

            Assert.Equal(doc.Id, saved.Id);
            var fromDb = await ctx.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == doc.Id);
            Assert.NotNull(fromDb);
        }

        [Fact]
        public async Task GetByIdAsync_Returns_Entity_When_Exists()
        {
            await using var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
            await using var ctx = CreateContext(connection);
            var doc = NewDoc();
            ctx.Documents.Add(doc);
            await ctx.SaveChangesAsync();

            var logger = Substitute.For<ILoggerWrapper<DocumentRepository>>();
            var repo = new DocumentRepository(ctx, logger);

            var result = await repo.GetByIdAsync(doc.Id);

            Assert.NotNull(result);
        }

        [Fact]
        public async Task GetByIdAsync_Returns_Null_When_Not_Found()
        {
            await using var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
            await using var ctx = CreateContext(connection);

            var logger = Substitute.For<ILoggerWrapper<DocumentRepository>>();
            var repo = new DocumentRepository(ctx, logger);

            var id = Guid.NewGuid();
            var result = await repo.GetByIdAsync(id);

            Assert.Null(result);
        }

        [Fact]
        public async Task GetAllAsync_Returns_Ordered_By_UploadDate_Desc()
        {
            await using var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
            await using var ctx = CreateContext(connection);
            var older = NewDoc(name: "older.pdf", uploaded: DateTime.UtcNow.AddDays(-2));
            var newer = NewDoc(name: "newer.pdf", uploaded: DateTime.UtcNow.AddDays(-1));
            ctx.Documents.AddRange(older, newer);
            await ctx.SaveChangesAsync();

            var logger = Substitute.For<ILoggerWrapper<DocumentRepository>>();
            var repo = new DocumentRepository(ctx, logger);

            var results = await repo.GetAllAsync();

            Assert.Equal(2, results.Count);
            Assert.Equal("newer.pdf", results.First().Name);
        }

        [Fact]
        public async Task GetAllAsync_Excludes_OcrText_And_GenAiSummary()
        {
            await using var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
            await using var ctx = CreateContext(connection);
            var doc = NewDoc();
            doc.OcrText = "This is OCR extracted text content";
            doc.GenAiSummary = "This is the AI generated summary";
            ctx.Documents.Add(doc);
            await ctx.SaveChangesAsync();

            var logger = Substitute.For<ILoggerWrapper<DocumentRepository>>();
            var repo = new DocumentRepository(ctx, logger);

            var results = await repo.GetAllAsync();

            Assert.Single(results);
            var result = results.First();
            // GetAllAsync should exclude these fields for performance
            Assert.Null(result.OcrText);
            Assert.Null(result.GenAiSummary);
            Assert.Equal(doc.Name, result.Name);
        }

        [Fact]
        public async Task GetAllWithContentAndSummaryAsync_Includes_OcrText_And_GenAiSummary()
        {
            await using var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
            await using var ctx = CreateContext(connection);
            var doc = NewDoc();
            doc.OcrText = "This is OCR extracted text content";
            doc.GenAiSummary = "This is the AI generated summary";
            ctx.Documents.Add(doc);
            await ctx.SaveChangesAsync();

            var logger = Substitute.For<ILoggerWrapper<DocumentRepository>>();
            var repo = new DocumentRepository(ctx, logger);

            var results = await repo.GetAllWithContentAndSummaryAsync();

            Assert.Single(results);
            var result = results.First();
            // GetAllWithContentAndSummaryAsync MUST include these fields for backup
            Assert.NotNull(result.OcrText);
            Assert.NotNull(result.GenAiSummary);
            Assert.Equal("This is OCR extracted text content", result.OcrText);
            Assert.Equal("This is the AI generated summary", result.GenAiSummary);
            Assert.Equal(doc.Name, result.Name);
        }

        [Fact]
        public async Task DeleteAsync_Removes_Entity_And_Returns_True()
        {
            await using var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
            await using var ctx = CreateContext(connection);
            var doc = NewDoc();
            ctx.Documents.Add(doc);
            await ctx.SaveChangesAsync();

            var logger = Substitute.For<ILoggerWrapper<DocumentRepository>>();
            var repo = new DocumentRepository(ctx, logger);

            var removed = await repo.DeleteAsync(doc.Id);
            Assert.True(removed);
        }

        [Fact]
        public async Task DeleteAsync_Returns_False_When_Not_Found()
        {
            await using var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
            await using var ctx = CreateContext(connection);

            var logger = Substitute.For<ILoggerWrapper<DocumentRepository>>();
            var repo = new DocumentRepository(ctx, logger);

            var id = Guid.NewGuid();
            var removed = await repo.DeleteAsync(id);
            Assert.False(removed);
        }

        [Fact]
        public async Task GetByIdsAsync_Returns_Matching_Documents()
        {
            await using var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
            await using var ctx = CreateContext(connection);
            var doc1 = NewDoc(name: "doc1.pdf");
            var doc2 = NewDoc(name: "doc2.pdf");
            var doc3 = NewDoc(name: "doc3.pdf");
            ctx.Documents.AddRange(doc1, doc2, doc3);
            await ctx.SaveChangesAsync();

            var logger = Substitute.For<ILoggerWrapper<DocumentRepository>>();
            var repo = new DocumentRepository(ctx, logger);

            var ids = new[] { doc1.Id, doc3.Id };
            var results = await repo.GetByIdsAsync(ids);

            Assert.Equal(2, results.Count);
            Assert.Contains(results, d => d.Id == doc1.Id);
            Assert.Contains(results, d => d.Id == doc3.Id);
            Assert.DoesNotContain(results, d => d.Id == doc2.Id);
        }

        [Fact]
        public async Task GetByIdsAsync_Returns_Empty_When_No_Matches()
        {
            await using var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
            await using var ctx = CreateContext(connection);

            var logger = Substitute.For<ILoggerWrapper<DocumentRepository>>();
            var repo = new DocumentRepository(ctx, logger);

            var ids = new[] { Guid.NewGuid(), Guid.NewGuid() };
            var results = await repo.GetByIdsAsync(ids);

            Assert.Empty(results);
        }

        [Fact]
        public async Task UpdateMetadataAsync_Updates_Both_Fields_Successfully()
        {
            await using var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
            await using var ctx = CreateContext(connection);
            var doc = NewDoc(name: "Original Name");
            doc.GenAiSummary = "Original Summary";
            ctx.Documents.Add(doc);
            await ctx.SaveChangesAsync();

            var logger = Substitute.For<ILoggerWrapper<DocumentRepository>>();
            var repo = new DocumentRepository(ctx, logger);

            var result = await repo.UpdateMetadataAsync(doc.Id, "New Name", "New Summary");

            Assert.True(result);
            var updated = await ctx.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == doc.Id);
            Assert.NotNull(updated);
            Assert.Equal("New Name", updated.Name);
            Assert.Equal("New Summary", updated.GenAiSummary);
        }

        [Fact]
        public async Task UpdateMetadataAsync_Updates_Only_Name_When_Summary_Null()
        {
            await using var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
            await using var ctx = CreateContext(connection);
            var doc = NewDoc(name: "Original Name");
            doc.GenAiSummary = "Original Summary";
            ctx.Documents.Add(doc);
            await ctx.SaveChangesAsync();

            var logger = Substitute.For<ILoggerWrapper<DocumentRepository>>();
            var repo = new DocumentRepository(ctx, logger);

            var result = await repo.UpdateMetadataAsync(doc.Id, "New Name", null);

            Assert.True(result);
            var updated = await ctx.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == doc.Id);
            Assert.NotNull(updated);
            Assert.Equal("New Name", updated.Name);
            Assert.Equal("Original Summary", updated.GenAiSummary); // Should remain unchanged
        }

        [Fact]
        public async Task UpdateMetadataAsync_Updates_Only_Summary_When_Name_Null()
        {
            await using var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
            await using var ctx = CreateContext(connection);
            var doc = NewDoc(name: "Original Name");
            doc.GenAiSummary = "Original Summary";
            ctx.Documents.Add(doc);
            await ctx.SaveChangesAsync();

            var logger = Substitute.For<ILoggerWrapper<DocumentRepository>>();
            var repo = new DocumentRepository(ctx, logger);

            var result = await repo.UpdateMetadataAsync(doc.Id, null, "New Summary");

            Assert.True(result);
            var updated = await ctx.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == doc.Id);
            Assert.NotNull(updated);
            Assert.Equal("Original Name", updated.Name); // Should remain unchanged
            Assert.Equal("New Summary", updated.GenAiSummary);
        }

        [Fact]
        public async Task UpdateMetadataAsync_Returns_False_When_Document_Not_Found()
        {
            await using var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
            await using var ctx = CreateContext(connection);

            var logger = Substitute.For<ILoggerWrapper<DocumentRepository>>();
            var repo = new DocumentRepository(ctx, logger);

            var id = Guid.NewGuid();
            var result = await repo.UpdateMetadataAsync(id, "New Name", "New Summary");

            Assert.False(result);
        }

        [Fact]
        public async Task UpdateMetadataAsync_Persists_Changes_To_Database()
        {
            await using var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
            await using var ctx = CreateContext(connection);
            var doc = NewDoc();
            doc.GenAiSummary = "Original";
            ctx.Documents.Add(doc);
            await ctx.SaveChangesAsync();

            var logger = Substitute.For<ILoggerWrapper<DocumentRepository>>();
            var repo = new DocumentRepository(ctx, logger);

            await repo.UpdateMetadataAsync(doc.Id, "Updated", "Updated Summary");

            // Query the database directly to verify persistence
            var fromDb = await ctx.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == doc.Id);
            Assert.NotNull(fromDb);
            Assert.Equal("Updated", fromDb.Name);
            Assert.Equal("Updated Summary", fromDb.GenAiSummary);
        }
    }
}