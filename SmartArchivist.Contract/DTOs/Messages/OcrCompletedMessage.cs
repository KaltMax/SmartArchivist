namespace SmartArchivist.Contract.DTOs.Messages
{
    /// <summary>
    /// Message published when OCR processing has been completed. -> Triggers GenAI summary generation.
    /// </summary>
    public class OcrCompletedMessage
    {
        public required Guid DocumentId { get; init; }
        public required string FileName { get; init; }
        public required string ExtractedText { get; init; }
    }
}
