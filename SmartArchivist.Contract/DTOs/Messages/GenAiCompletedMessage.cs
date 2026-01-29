namespace SmartArchivist.Contract.DTOs.Messages
{
    /// <summary>
    /// Message when GenAI processing has been completed.
    /// </summary>
    public class GenAiCompletedMessage
    {
        public required Guid DocumentId { get; init; }
        public required string FileName { get; init; }
        public required string ExtractedText { get; init; }
        public required string Summary { get; init; }
        public required string[] Tags { get; init; }
    }
}
