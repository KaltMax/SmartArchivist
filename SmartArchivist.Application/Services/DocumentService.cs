using AutoMapper;
using SmartArchivist.Application.DomainModels;
using SmartArchivist.Application.Exceptions;
using SmartArchivist.Contract.Abstractions.Messaging;
using SmartArchivist.Contract.Abstractions.Storage;
using SmartArchivist.Contract.Enums;
using SmartArchivist.Dal.Entities;
using SmartArchivist.Dal.Repositories;
using SmartArchivist.Dal.Exceptions;
using SmartArchivist.Contract.Logger;
using SmartArchivist.Contract;
using SmartArchivist.Contract.DTOs.Messages;
using SmartArchivist.Contract.Abstractions.Search;

namespace SmartArchivist.Application.Services
{
    /// <summary>
    /// Provides document management operations including creation, retrieval, update, deletion, and search
    /// functionality for documents and their associated files.
    /// </summary>
    public class DocumentService : IDocumentService
    {
        private readonly IMapper _mapper;
        private readonly IDocumentRepository _documentRepository;
        private readonly ILoggerWrapper<DocumentService> _logger;
        private readonly IRabbitMqPublisher _publisher;
        private readonly IFileStorageService _fileStorageService;
        private readonly IIndexingService _indexingService;
        private readonly ISearchService _searchService;

        public DocumentService(
            IMapper mapper,
            IDocumentRepository documentRepository,
            ILoggerWrapper<DocumentService> logger,
            IRabbitMqPublisher publisher,
            IFileStorageService fileStorageService,
            IIndexingService indexingService,
            ISearchService searchService)
        {
            _mapper = mapper;
            _documentRepository = documentRepository;
            _logger = logger;
            _publisher = publisher;
            _fileStorageService = fileStorageService;
            _indexingService = indexingService;
            _searchService = searchService;
        }

        public async Task<DocumentDomain> CreateDocumentAsync(DocumentDomain documentDto, byte[] fileContent)
        {
            _logger.LogDebug("Starting document creation for {DocumentName}", documentDto.Name);

            string? storedPath = null;

            try
            {
                var document = _mapper.Map<DocumentDomain>(documentDto);
                document.Id = Guid.NewGuid();
                document.UploadDate = DateTime.UtcNow;
                document.FileSize = fileContent.Length;

                // Create file name (sanitization handled by storage service)
                var fileName = $"{document.Name}{document.FileExtension}";

                // Upload to storage service -> it handles sanitization and returns the stored path
                storedPath = await _fileStorageService.UploadFileAsync(
                    document.Id,
                    fileName,
                    fileContent,
                    document.ContentType
                );

                // Store the path returned by storage service
                document.FilePath = storedPath;

                _logger.LogDebug("File uploaded to storage, path: {StoredPath}", storedPath);

                // Save to database
                var entity = _mapper.Map<DocumentEntity>(document);
                var saved = await _documentRepository.AddAsync(entity);

                // Publish to RabbitMQ
                await _publisher.PublishAsync(
                    new DocumentUploadedMessage
                    {
                        DocumentId = document.Id,
                        FileName = fileName,
                        StoragePath = storedPath,
                        ContentType = document.ContentType
                    },
                    QueueNames.OcrQueue
                );

                _logger.LogInformation("Document created successfully with ID {DocumentId}", saved.Id);
                return _mapper.Map<DocumentDomain>(saved);
            }
            catch (DocumentAlreadyExistsException ex)
            {
                _logger.LogError(ex, "Duplicate document creation attempt: {DocumentName}", documentDto.Name);
                throw new DuplicateDocumentException($"A document with the name {documentDto.Name} already exists.", ex);
            }
            catch (DocumentRepositoryException ex)
            {
                // Cleanup storage file if DB fails
                if (storedPath != null)
                {
                    try
                    {
                        await _fileStorageService.DeleteFileAsync(storedPath);
                        _logger.LogInformation("Cleaned up file from storage: {StoredPath}", storedPath);
                    }
                    catch (Exception deleteEx)
                    {
                        _logger.LogWarning(deleteEx, "Failed to cleanup file: {StoredPath}", storedPath);
                    }
                }
                _logger.LogError(ex, "Database error while saving document");
                throw new DocumentProcessingException("Failed to save document to database", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Storage error during file upload");
                throw new FileOperationException("File storage operation failed", ex);
            }
        }

        public async Task<IEnumerable<DocumentDomain>> GetAllDocumentsAsync()
        {
            _logger.LogDebug("Retrieving all documents");

            try
            {
                var entities = await _documentRepository.GetAllAsync();
                _logger.LogInformation("Retrieved {DocumentCount} documents", entities.Count);
                return _mapper.Map<IEnumerable<DocumentDomain>>(entities);
            }
            catch (DocumentRepositoryException ex)
            {
                _logger.LogError(ex, "Failed to retrieve all documents");
                throw new DocumentProcessingException("Failed to retrieve documents from database", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during document retrieval");
                throw new DocumentProcessingException("An unexpected error occurred while retrieving documents", ex);
            }
        }

        public async Task<DocumentDomain?> GetDocumentByIdAsync(Guid id)
        {
            _logger.LogDebug("Retrieving document with ID {DocumentId}", id);

            try
            {
                var entity = await _documentRepository.GetByIdAsync(id);

                if (entity == null)
                {
                    _logger.LogError("Document with ID {DocumentId} not found", id);
                    return null;
                }

                _logger.LogInformation("Document with ID {DocumentId} retrieved successfully", id);
                return _mapper.Map<DocumentDomain>(entity);
            }
            catch (DocumentRepositoryException ex)
            {
                _logger.LogError(ex, "Failed to retrieve the document with document ID: {id}", id);
                throw new DocumentRetrievalException($"Failed to retrieve document with document ID {id} from database", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected Error while retrieving the document with document ID: {id}", id);
                throw new DocumentRetrievalException($"Unexpected error while retrieving the document with document ID: {id}", ex);
            }
        }

        public async Task<Stream> GetDocumentFileAsync(Guid id)
        {
            _logger.LogDebug("Retrieving file for document {DocumentId}", id);

            try
            {
                var document = await _documentRepository.GetByIdAsync(id);
                if (document == null || string.IsNullOrEmpty(document.FilePath))
                {
                    _logger.LogError("Document {DocumentId} not found or has no file path", id);
                    throw new FileNotFoundException($"Document {id} not found");
                }

                // Download from MinIO
                var fileStream = await _fileStorageService.DownloadFileAsync(document.FilePath);

                _logger.LogInformation("File retrieved successfully for document {DocumentId}", id);
                return fileStream;
            }
            catch (DocumentRepositoryException ex)
            {
                _logger.LogError(ex, "Failed to retrieve document metadata for {DocumentId}", id);
                throw new DocumentRetrievalException($"Failed to retrieve document {id} metadata", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Storage error while downloading file for document {DocumentId}", id);
                throw new FileOperationException($"Error accessing file for document {id}", ex);
            }
        }

        public async Task<bool> UpdateDocumentStateAsync(Guid id, DocumentState state)
        {
            _logger.LogDebug("Updating document {DocumentId} state to {State}", id, state);

            try
            {
                var result = await _documentRepository.UpdateStateAsync(id, state);
                if (result)
                {
                    _logger.LogInformation("Document {DocumentId} state updated to {State} successfully", id, state);
                }
                else
                {
                    _logger.LogError("Failed to update document {DocumentId} state", id);
                }
                return result;
            }
            catch (DocumentRepositoryException ex)
            {
                _logger.LogError(ex, "Failed to update document {DocumentId} state in database", id);
                throw new DocumentUpdateException($"Failed to update document {id} state", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error updating document {DocumentId} state", id);
                throw new DocumentUpdateException($"Unexpected error updating document {id} state", ex);
            }
        }

        public async Task<bool> DeleteDocumentAsync(Guid id)
        {
            _logger.LogDebug("Starting deletion of document {DocumentId}", id);

            try
            {
                var document = await _documentRepository.GetByIdAsync(id);

                // Delete file from MinIO
                if (document != null && !string.IsNullOrEmpty(document.FilePath))
                {
                    try
                    {
                        await _fileStorageService.DeleteFileAsync(document.FilePath);
                        _logger.LogInformation("File deleted from MinIO for document {DocumentId}", id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete file from MinIO, continuing with DB deletion");
                    }
                }

                // Delete from database
                var result = await _documentRepository.DeleteAsync(id);
                if (result)
                {
                    _logger.LogInformation("Document {DocumentId} deleted successfully", id);

                    try
                    {
                        await _indexingService.DeleteDocumentIndexAsync(id);
                        _logger.LogInformation("Document {DocumentId} removed from search index", id);

                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to remove document {DocumentId} from search index", id);
                    }
                }

                return result;
            }
            catch (DocumentRepositoryException ex)
            {
                _logger.LogError(ex, "Failed to delete document {DocumentId} from database", id);
                throw new DocumentProcessingException($"Failed to delete document {id}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error deleting document {DocumentId}", id);
                throw new DocumentProcessingException($"Unexpected error deleting document {id}", ex);
            }
        }

        public async Task<DocumentDomain?> UpdateDocumentMetadataAsync(Guid id, string? name, string? summary)
        {
            _logger.LogDebug("Updating metadata for document {DocumentId}", id);

            try
            {
                // Validate at least one field is provided
                if (name == null && summary == null)
                {
                    _logger.LogWarning("UpdateDocumentMetadataAsync called with no fields to update for document {DocumentId}", id);
                    throw new ArgumentException("At least one field (name or summary) must be provided for update.");
                }

                // Update in database
                var success = await _documentRepository.UpdateMetadataAsync(id, name, summary);
                if (!success)
                {
                    _logger.LogError("Document {DocumentId} not found during metadata update", id);
                    return null;
                }

                // Get updated document to re-index
                var document = await _documentRepository.GetByIdAsync(id);
                if (document == null)
                {
                    _logger.LogError("Document {DocumentId} not found after metadata update", id);
                    return null;
                }

                // Re-index in ElasticSearch with updated document metadata
                await _indexingService.IndexDocumentAsync(
                    document.Id,
                    document.Name,
                    document.OcrText ?? "",
                    document.GenAiSummary ?? "",
                    document.Tags ?? []
                );

                _logger.LogInformation("Document {DocumentId} metadata updated and re-indexed successfully", id);
                
                return _mapper.Map<DocumentDomain>(document);
            }
            catch (DocumentRepositoryException ex)
            {
                _logger.LogError(ex, "Failed to update document {DocumentId} metadata in database", id);
                throw new DocumentUpdateException($"Failed to update document {id} metadata", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error updating document {DocumentId} metadata", id);
                throw new DocumentUpdateException($"Unexpected error updating document {id} metadata", ex);
            }
        }

        public async Task<IEnumerable<DocumentDomain>> SearchDocumentsAsync(string query)
        {
            _logger.LogDebug("Searching documents with query: {SearchQuery}", query);

            try
            {
                // Use ElasticSearch to get matching document IDs
                var documentIds = await _searchService.SearchDocumentsAsync(query);

                if (!documentIds.Any())
                {
                    _logger.LogInformation("Search completed. No documents found for query: {SearchQuery}", query);
                    return Enumerable.Empty<DocumentDomain>();
                }

                // Fetch full document details from repository
                var entities = await _documentRepository.GetByIdsAsync(documentIds);
                
                _logger.LogInformation("Search completed. Found {ResultCount} documents", entities.Count);
                return _mapper.Map<IEnumerable<DocumentDomain>>(entities);
            }
            catch (DocumentRepositoryException ex)
            {
                _logger.LogError(ex, "Failed to retrieve documents from database during search with query: {SearchQuery}", query);
                throw new DocumentProcessingException($"Failed to retrieve documents for search query: {query}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to search documents with query: {SearchQuery}", query);
                throw new DocumentProcessingException($"Failed to search documents with query: {query}", ex);
            }
        }
    }
}