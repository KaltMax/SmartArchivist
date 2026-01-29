using NSubstitute;
using SmartArchivist.Contract.Logger;
using SmartArchivist.Infrastructure.ElasticSearch;

namespace Tests.SmartArchivist.InfrastructureTests
{
    /// <summary>
    /// Unit tests for ElasticSearchService focusing on input validation.
    /// </summary>
    public class ElasticSearchServiceTests
    {
        private readonly ILoggerWrapper<ElasticSearchService> _logger;
        private readonly ElasticSearchConfig _config;

        public ElasticSearchServiceTests()
        {
            _logger = Substitute.For<ILoggerWrapper<ElasticSearchService>>();
            _config = new ElasticSearchConfig
            {
                Url = "http://localhost:9200",
                IndexName = "test-documents",
                MaxSearchResults = 100
            };
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public async Task SearchDocuments_EmptyQuery_ThrowsArgumentException(string? invalidQuery)
        {
            // Arrange
            var sut = new ElasticSearchService(_logger, _config);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                sut.SearchDocumentsAsync(invalidQuery!));
        }
    }
}