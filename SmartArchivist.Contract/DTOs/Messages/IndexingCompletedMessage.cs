namespace SmartArchivist.Contract.DTOs.Messages
{
    /// <summary>
    /// Represents a message indicating that indexing has completed for a specific document.
    /// </summary>
    public class IndexingCompletedMessage
    {
        public required Guid DocumentId { get; init; }
        public required string FileName { get; init; }
    }
}
