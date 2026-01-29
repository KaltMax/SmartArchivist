namespace SmartArchivist.Contract.Abstractions.Search
{
    /// <summary>
    /// Defines the contract for a service that provides document indexing functionality.
    /// </summary>
    public interface IIndexingService
    {
        Task InitializeAsync();
        Task IndexDocumentAsync(Guid documentId, string fileName, string extractedText, string summary, string[] tags);
        Task DeleteDocumentIndexAsync(Guid documentId);
    }
}
