using Microsoft.EntityFrameworkCore;
using SmartArchivist.Contract.DTOs;
using SmartArchivist.Contract.Enums;
using SmartArchivist.Contract.Logger;
using SmartArchivist.Dal.Data;
using SmartArchivist.Dal.Entities;
using SmartArchivist.Dal.Exceptions;

namespace SmartArchivist.Dal.Repositories
{
    /// <summary>
    /// Provides methods for managing and querying document entities in the underlying database, including creation,
    /// retrieval, update, deletion, and search operations.
    /// </summary>
    public class DocumentRepository : IDocumentRepository
    {
        private readonly SmartArchivistDbContext _db;
        private readonly ILoggerWrapper<DocumentRepository> _logger;

        public DocumentRepository(SmartArchivistDbContext db, ILoggerWrapper<DocumentRepository> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<DocumentEntity> AddAsync(DocumentEntity entity)
        {
            _logger.LogDebug("AddAsync starting for document {DocumentId} Name={Name}", entity.Id, entity.Name);

            try
            {
                _db.Documents.Add(entity);
                await _db.SaveChangesAsync();
                _logger.LogInformation("AddAsync succeeded for document {DocumentId} Name={Name}", entity.Id, entity.Name);
                return entity;
            }
            catch (DbUpdateException ex)
            {
                // Check if it's a unique constraint violation on the Name field
                var innerException = ex.InnerException?.Message ?? string.Empty;
                if (innerException.Contains("duplicate", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning(ex, "Duplicate document name detected: {Name}", entity.Name);
                    throw new DocumentAlreadyExistsException($"A document with the name '{entity.Name}' already exists.", ex);
                }

                _logger.LogError(ex, "AddAsync failed (DbUpdateException) for document {DocumentId} Name={Name}", entity.Id, entity.Name);
                throw new DocumentRepositoryException("Failed to add document (database update error).", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AddAsync unexpected error for document {DocumentId} Name={Name}", entity.Id, entity.Name);
                throw new DocumentRepositoryException("Unexpected error while adding document.", ex);
            }
        }

        public async Task<DocumentEntity?> GetByIdAsync(Guid id)
        {
            _logger.LogDebug("GetByIdAsync retrieving document {DocumentId}", id);
            try
            {
                var doc = await _db.Documents
                    .AsNoTracking()
                    .FirstOrDefaultAsync(d => d.Id == id);

                if (doc == null)
                    _logger.LogDebug("GetByIdAsync document {DocumentId} not found", id);
                else
                    _logger.LogDebug("GetByIdAsync document {DocumentId} found (Name={Name})", id, doc.Name);

                return doc;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetByIdAsync unexpected error for document {DocumentId}", id);
                throw new DocumentRepositoryException($"Unexpected error while retrieving document Id={id}.", ex);
            }
        }

        public async Task<IReadOnlyList<DocumentEntity>> GetByIdsAsync(IEnumerable<Guid> ids)
        {
            _logger.LogDebug("GetByIdsAsync retrieving documents by IDs");
            try
            {
                var idList = ids.ToList();
                var documents = await _db.Documents
                    .AsNoTracking()
                    .Where(d => idList.Contains(d.Id))
                    .Select(d => new DocumentEntity
                    {
                        Id = d.Id,
                        Name = d.Name,
                        FilePath = d.FilePath,
                        FileExtension = d.FileExtension,
                        ContentType = d.ContentType,
                        UploadDate = d.UploadDate,
                        FileSize = d.FileSize,
                        State = d.State,
                        Tags = d.Tags
                        // OcrText and GenAiSummary are excluded for performance reasons
                    })
                    .ToListAsync();

                _logger.LogInformation("GetByIdsAsync retrieved {Count} documents out of {RequestedCount} IDs", documents.Count, idList.Count);
                return documents;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetByIdsAsync unexpected error");
                throw new DocumentRepositoryException("Unexpected error while retrieving documents by IDs.", ex);
            }
        }

        public async Task<IReadOnlyList<DocumentEntity>> GetAllAsync()
        {
            _logger.LogDebug("GetAllAsync retrieving all documents");
            try
            {
                var list = await _db.Documents
                    .AsNoTracking()
                    .Select(d => new DocumentEntity
                    {
                        Id = d.Id,
                        Name = d.Name,
                        FilePath = d.FilePath,
                        FileExtension = d.FileExtension,
                        ContentType = d.ContentType,
                        UploadDate = d.UploadDate,
                        FileSize = d.FileSize,
                        State = d.State,
                        Tags = d.Tags
                        // OcrText and GenAiSummary are excluded for performance reasons
                    })
                    .OrderByDescending(d => d.UploadDate)
                    .ToListAsync();

                _logger.LogInformation("GetAllAsync retrieved {Count} documents", list.Count);
                return list;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetAllAsync unexpected error");
                throw new DocumentRepositoryException("Unexpected error while retrieving all documents.", ex);
            }
        }

        public async Task<IReadOnlyList<DocumentEntity>> GetAllWithContentAndSummaryAsync()
        {
            _logger.LogDebug("GetAllWithContentAsync retrieving all documents with full content");
            try
            {
                var list = await _db.Documents
                    .AsNoTracking()
                    .OrderByDescending(d => d.UploadDate)
                    .ToListAsync();

                _logger.LogInformation("GetAllWithContentAsync retrieved {Count} documents with full content", list.Count);
                return list;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetAllWithContentAsync unexpected error");
                throw new DocumentRepositoryException("Unexpected error while retrieving all documents with content.", ex);
            }
        }

        public async Task<bool> UpdateOcrTextAsync(Guid id, string? ocrText)
        {
            _logger.LogDebug("UpdateOcrTextAsync starting for document {DocumentId}", id);

            try
            {
                var existing = await _db.Documents.FirstOrDefaultAsync(d => d.Id == id);
                if (existing == null)
                {
                    _logger.LogWarning("UpdateOcrTextAsync document {DocumentId} not found", id);
                    return false;
                }

                existing.OcrText = ocrText;
                existing.State = DocumentState.OcrCompleted;

                await _db.SaveChangesAsync();
                _logger.LogInformation("UpdateOcrTextAsync succeeded for document {DocumentId}", id);
                return true;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "UpdateOcrTextAsync failed (DbUpdateException) for document {DocumentId}", id);
                throw new DocumentRepositoryException($"Failed to update document Id={id} (database update error).", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UpdateOcrTextAsync unexpected error for document {DocumentId}", id);
                throw new DocumentRepositoryException($"Unexpected error while updating document Id={id}.", ex);
            }
        }

        public async Task<bool> UpdateGenAiResultAsync(Guid id, GenAiResult genAiResult)
        {
            _logger.LogDebug("UpdateGenAiSummaryAsync starting for document {DocumentId}", id);

            try
            {
                var existing = await _db.Documents.FirstOrDefaultAsync(d => d.Id == id);
                if (existing == null)
                {
                    _logger.LogWarning("UpdateGenAiSummaryAsync document {DocumentId} not found", id);
                    return false;
                }

                existing.GenAiSummary = genAiResult.Summary;
                existing.Tags = genAiResult.Tags;
                existing.State = DocumentState.GenAiCompleted;

                await _db.SaveChangesAsync();
                _logger.LogInformation("UpdateGenAiSummaryAsync succeeded for document {DocumentId}", id);
                return true;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "UpdateGenAiSummaryAsync failed (DbUpdateException) for document {DocumentId}", id);
                throw new DocumentRepositoryException($"Failed to update document Id={id} (database update error).", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UpdateGenAiSummaryAsync unexpected error for document {DocumentId}", id);
                throw new DocumentRepositoryException($"Unexpected error while updating document Id={id}.", ex);
            }
        }

        public async Task<bool> UpdateStateAsync(Guid id, DocumentState state)
        {
            _logger.LogDebug("UpdateStateAsync starting for document {DocumentId} to state {State}", id, state);

            try
            {
                var existing = await _db.Documents.FirstOrDefaultAsync(d => d.Id == id);
                if (existing == null)
                {
                    _logger.LogWarning("UpdateStateAsync document {DocumentId} not found", id);
                    return false;
                }

                existing.State = state;

                await _db.SaveChangesAsync();
                _logger.LogInformation("UpdateStateAsync succeeded for document {DocumentId}, new state: {State}", id, state);
                return true;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "UpdateStateAsync failed (DbUpdateException) for document {DocumentId}", id);
                throw new DocumentRepositoryException($"Failed to update document Id={id} state (database update error).", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UpdateStateAsync unexpected error for document {DocumentId}", id);
                throw new DocumentRepositoryException($"Unexpected error while updating document Id={id} state.", ex);
            }
        }

        public async Task<bool> UpdateMetadataAsync(Guid id, string? name, string? summary)
        {
            _logger.LogDebug("UpdateMetadataAsync starting for document {DocumentId}", id);

            try
            {
                var existing = await _db.Documents.FirstOrDefaultAsync(d => d.Id == id);
                if (existing == null)
                {
                    _logger.LogWarning("UpdateMetadataAsync document {DocumentId} not found", id);
                    return false;
                }

                if (name != null)
                {
                    existing.Name = name;
                }

                if (summary != null)
                {
                    existing.GenAiSummary = summary;
                }

                await _db.SaveChangesAsync();
                _logger.LogInformation("UpdateMetadataAsync succeeded for document {DocumentId}", id);
                return true;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "UpdateMetadataAsync failed (DbUpdateException) for document {DocumentId}", id);
                throw new DocumentRepositoryException($"Failed to update document Id={id} metadata (database update error).", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UpdateMetadataAsync unexpected error for document {DocumentId}", id);
                throw new DocumentRepositoryException($"Unexpected error while updating document Id={id} metadata.", ex);
            }
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            _logger.LogDebug("DeleteAsync attempting delete for document {DocumentId}", id);

            try
            {
                var entity = await _db.Documents.FirstOrDefaultAsync(d => d.Id == id);
                if (entity == null)
                {
                    _logger.LogInformation("DeleteAsync document {DocumentId} not found (no action taken)", id);
                    return false;
                }

                _db.Documents.Remove(entity);
                await _db.SaveChangesAsync();
                _logger.LogInformation("DeleteAsync succeeded for document {DocumentId}", id);
                return true;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "DeleteAsync failed (DbUpdateException) for document {DocumentId}", id);
                throw new DocumentRepositoryException($"Failed to delete document Id={id} (database update error).", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DeleteAsync unexpected error for document {DocumentId}", id);
                throw new DocumentRepositoryException($"Unexpected error while deleting document Id={id}.", ex);
            }
        }
    }
}