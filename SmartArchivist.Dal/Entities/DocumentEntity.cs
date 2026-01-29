using SmartArchivist.Contract.Enums;
using System.ComponentModel.DataAnnotations;

namespace SmartArchivist.Dal.Entities
{
    /// <summary>
    /// Represents a document and its associated metadata in the database.
    /// </summary>
    public class DocumentEntity
    {
        [Key]
        public Guid Id { get; set; }
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;
        [MaxLength(2048)]
        public string FilePath { get; set; } = string.Empty;
        [MaxLength(32)]
        public string FileExtension { get; set; } = string.Empty;
        [MaxLength(128)]
        public string ContentType { get; set; } = string.Empty;
        public DateTime UploadDate { get; set; }
        public long FileSize { get; set; }
        public DocumentState State { get; set; } = DocumentState.Uploaded;
        public string? OcrText { get; set; }
        public string? GenAiSummary { get; set; }
        public string[]? Tags { get; set; }
    }
}