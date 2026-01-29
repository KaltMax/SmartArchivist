using SmartArchivist.Contract.Enums;

namespace SmartArchivist.Application.DomainModels
{
    /// <summary>
    /// Represents a document in the business logic layer.
    /// </summary>
    public class DocumentDomain
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string FileExtension { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public DateTime UploadDate { get; set; }
        public long FileSize { get; set; }
        public DocumentState State { get; set; } = DocumentState.Uploaded;
        public string? OcrText { get; set; }
        public string? GenAiSummary { get; set; }
        public string[]? Tags { get; set; }
    }
}
