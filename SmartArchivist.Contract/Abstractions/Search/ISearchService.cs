namespace SmartArchivist.Contract.Abstractions.Search
{
    /// <summary>
    /// Abstraction for search service implementations.
    /// </summary>
    public interface ISearchService
    {
        Task<IEnumerable<Guid>> SearchDocumentsAsync(string query);
    }
}
