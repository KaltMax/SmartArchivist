using SmartArchivist.Dal.Entities;
using SmartArchivist.Contract.Enums;
using SmartArchivist.Contract.DTOs;

namespace SmartArchivist.Dal.Repositories
{
    /// <summary>
    /// Defines a contract for managing and querying document entities in a database.
    /// </summary>
    public interface IDocumentRepository
    {
        Task<DocumentEntity> AddAsync(DocumentEntity entity);
        Task<DocumentEntity?> GetByIdAsync(Guid id);
        Task<IReadOnlyList<DocumentEntity>> GetByIdsAsync(IEnumerable<Guid> ids);
        Task<IReadOnlyList<DocumentEntity>> GetAllAsync();
        Task<IReadOnlyList<DocumentEntity>> GetAllWithContentAndSummaryAsync();
        Task<bool> UpdateOcrTextAsync(Guid id, string? ocrText);
        Task<bool> UpdateGenAiResultAsync(Guid id, GenAiResult genAiResult);
        Task<bool> UpdateStateAsync(Guid id, DocumentState state);
        Task<bool> UpdateMetadataAsync(Guid id, string? name, string? summary);
        Task<bool> DeleteAsync(Guid id);
    }
}