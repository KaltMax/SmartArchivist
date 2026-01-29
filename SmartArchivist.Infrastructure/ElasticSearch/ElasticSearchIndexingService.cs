using Elastic.Clients.Elasticsearch;
using SmartArchivist.Contract.Abstractions.Search;
using SmartArchivist.Contract.Logger;

namespace SmartArchivist.Infrastructure.ElasticSearch
{
    /// <summary>
    /// Provides indexing and deletion operations for documents in an ElasticSearch index.
    /// </summary>
    public class ElasticSearchIndexingService : IIndexingService
    {
        private readonly ILoggerWrapper<ElasticSearchIndexingService> _logger;
        private readonly ElasticSearchConfig _config;
        private readonly ElasticsearchClient _client;

        public ElasticSearchIndexingService(
            ILoggerWrapper<ElasticSearchIndexingService> logger,
            ElasticSearchConfig config)
        {
            _logger = logger;
            _config = config;

            var settings = new ElasticsearchClientSettings(new Uri(_config.Url))
                .DefaultIndex(_config.IndexName);

            _client = new ElasticsearchClient(settings);
        }

        public async Task InitializeAsync()
        {
            _logger.LogInformation("Initializing ElasticSearch index {IndexName} at {Url}", _config.IndexName, _config.Url);

            try
            {
                // Check if index already exists
                var existsResponse = await _client.Indices.ExistsAsync(_config.IndexName);

                if (existsResponse.Exists)
                {
                    _logger.LogInformation("ElasticSearch index {IndexName} already exists", _config.IndexName);
                    return;
                }

                // Create index with mappings for document fields
                var createResponse = await _client.Indices.CreateAsync(_config.IndexName, c => c
                    .Mappings(m => m
                        .Properties<object>(p => p
                            .Keyword("documentId")
                            .Text("fileName")
                            .Text("extractedText")
                            .Text("summary")
                            .Keyword("tags")
                            .Date("indexedAt")
                        )
                    )
                );

                if (!createResponse.IsValidResponse)
                {
                    _logger.LogError("Failed to create ElasticSearch index {IndexName}. Error: {Error}",
                        _config.IndexName, createResponse.DebugInformation);
                    throw new Exception($"Failed to create ElasticSearch index {_config.IndexName}: {createResponse.DebugInformation}");
                }

                _logger.LogInformation("Successfully created ElasticSearch index {IndexName}", _config.IndexName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing ElasticSearch index {IndexName}", _config.IndexName);
                throw;
            }
        }

        public async Task IndexDocumentAsync(Guid documentId, string fileName, string extractedText, string summary, string[] tags)
        {
            _logger.LogInformation(
                "Indexing document {DocumentId} into ElasticSearch at {Url} in index {IndexName}",
                documentId,
                _config.Url,
                _config.IndexName);

            try
            {
                var document = new
                {
                    documentId = documentId.ToString(),
                    fileName,
                    extractedText,
                    summary,
                    tags,
                    indexedAt = DateTime.UtcNow
                };

                var response = await _client.IndexAsync(document, _config.IndexName, documentId.ToString());

                if (!response.IsValidResponse)
                {
                    _logger.LogError("Failed to index document {documentId}. Error: {Error}", documentId, response.DebugInformation);
                    throw new Exception($"Failed to index document {documentId}: {response.DebugInformation}");
                }

                _logger.LogInformation("Successfully indexed {documentId} with result {Result}", documentId, response.Result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error indexing document {DocumentId}", documentId);
                throw;
            }
        }

        public async Task DeleteDocumentIndexAsync(Guid documentId)
        {
            _logger.LogInformation("Deleting document {DocumentId} from ElasticSearch index {IndexName}", documentId, _config.IndexName);

            try
            {
                var response = await _client.DeleteAsync(_config.IndexName, documentId.ToString());

                if (!response.IsValidResponse)
                {
                    if (response.ApiCallDetails.HttpStatusCode == 404)
                    {
                        _logger.LogWarning("Document {DocumentId} not found in index.", documentId);
                        return;
                    }

                    _logger.LogError("Failed to delete document {DocumentId}. Error: {Error}", documentId, response.DebugInformation);
                    throw new Exception($"Failed to delete document {documentId}: {response.DebugInformation}");
                }

                _logger.LogInformation("Successfully deleted document {DocumentId} with result {Result}", documentId, response.Result);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting document {DocumentId} from ElasticSearch", documentId);
                throw;
            }
        }
    }
}
