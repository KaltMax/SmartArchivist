using Elastic.Clients.Elasticsearch;
using SmartArchivist.Contract.Abstractions.Search;
using SmartArchivist.Contract.Logger;

namespace SmartArchivist.Infrastructure.ElasticSearch
{
    /// <summary>
    /// Provides search functionality using an ElasticSearch backend.
    /// </summary>
    public class ElasticSearchService : ISearchService
    {
        private readonly ILoggerWrapper<ElasticSearchService> _logger;
        private readonly ElasticSearchConfig _config;
        private readonly ElasticsearchClient _client;

        public ElasticSearchService(
            ILoggerWrapper<ElasticSearchService> logger,
            ElasticSearchConfig config)
        {
            _logger = logger;
            _config = config;

            var settings = new ElasticsearchClientSettings(new Uri(_config.Url))
                .DefaultIndex(_config.IndexName);

            _client = new ElasticsearchClient(settings);
        }

        public async Task<IEnumerable<Guid>> SearchDocumentsAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                throw new ArgumentException("Search query cannot be null or empty", nameof(query));
            }

            _logger.LogInformation("Searching documents with query: {Query} in index {IndexName}", query, _config.IndexName);

            try
            {
                var response = await _client.SearchAsync<object>(s => s
                    .Indices(_config.IndexName)
                    .Size(_config.MaxSearchResults)
                    .Query(q => q
                        .Bool(b => b
                            .Should(
                                // Exact and fuzzy matches
                                sh => sh.MultiMatch(m => m
                                    .Query(query)
                                    .Fields(new[] { "fileName", "extractedText", "summary", "tags" })
                                    .Fuzziness(new Fuzziness("AUTO"))
                                    .Type(Elastic.Clients.Elasticsearch.QueryDsl.TextQueryType.BestFields)
                                ),
                                // Wildcard for partial matches
                                sh => sh.Wildcard(w => w
                                    .Field("fileName")
                                    .Value($"*{query}*")
                                    .CaseInsensitive(true)
                                ),
                                sh => sh.Wildcard(w => w
                                    .Field("extractedText")
                                    .Value($"*{query}*")
                                    .CaseInsensitive(true)
                                ),
                                sh => sh.Wildcard(w => w
                                    .Field("summary")
                                    .Value($"*{query}*")
                                    .CaseInsensitive(true)
                                ),
                                sh => sh.Wildcard(w => w
                                    .Field("tags")
                                    .Value($"*{query}*")
                                    .CaseInsensitive(true)
                                )
                            )
                            .MinimumShouldMatch(1)
                        )
                    )
                );

                if (!response.IsValidResponse)
                {
                    _logger.LogError("Failed to search documents. Error: {Error}", response.DebugInformation);
                    throw new InvalidOperationException($"Failed to search documents: {response.DebugInformation}");
                }

                // Extract document IDs from hit metadata
                var documentIds = response.Hits
                    .Select(hit => Guid.Parse(hit.Id))
                    .ToList();

                _logger.LogInformation("Search completed. Found {ResultCount} documents", documentIds.Count);

                return documentIds;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching documents with query: {Query}", query);
                throw;
            }
        }
    }
}
