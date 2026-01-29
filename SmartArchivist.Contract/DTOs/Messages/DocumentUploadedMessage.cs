namespace SmartArchivist.Contract.DTOs.Messages
{
    /// <summary>
    /// Message published when a document has been uploaded and stored -> Triggers OCR processing.
    /// </summary>
    public class DocumentUploadedMessage
    {
        public required Guid DocumentId { get; init; }
        public required string FileName { get; init; }
        public required string StoragePath { get; init; } // Path in the storage service
        public required string ContentType { get; init; } // MIME type
    }
}
