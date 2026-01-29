namespace SmartArchivist.Contract.DTOs
{
    /// <summary>
    /// Data transfer object representing a document in the SmartArchivist system.
    /// </summary>
    public class DocumentDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string FileExtension { get; set; } = string.Empty;
        public string ContentType { get; set;  } = string.Empty;
        public DateTime UploadDate { get; set; }
        public long FileSize { get; set; }
        public Enums.DocumentState State { get; set; } = Enums.DocumentState.Uploaded;
        public string? OcrText { get; set; }
        public string? GenAiSummary { get; set; }
        public string[]? Tags { get; set; }
    }
}
