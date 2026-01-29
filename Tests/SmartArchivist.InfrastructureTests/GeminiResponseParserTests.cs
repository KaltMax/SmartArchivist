using NSubstitute;
using SmartArchivist.Contract.Logger;
using SmartArchivist.Infrastructure.GenAi;

namespace Tests.SmartArchivist.InfrastructureTests
{
    public class GeminiResponseParserTests
    {
        private readonly GeminiResponseParser _responseParser;
        private readonly ILoggerWrapper<GeminiResponseParser> _logger;

        public GeminiResponseParserTests()
        {
            _logger = Substitute.For<ILoggerWrapper<GeminiResponseParser>>();
            _responseParser = new GeminiResponseParser(_logger);
        }

        [Fact]
        public void ParseResponse_ValidJsonWithSummaryAndTags_ReturnsGenAiResult()
        {
            // Arrange
            var json = @"{
                ""candidates"": [{
                    ""content"": {
                        ""parts"": [{
                            ""text"": ""{\""summary\"": \""Test summary\"", \""tags\"": [\""tag1\"", \""tag2\""]}""
                        }]
                    }
                }]
            }";

            // Act
            var result = _responseParser.ParseResponse(json);

            // Assert
            Assert.Equal("Test summary", result.Summary);
            Assert.Equal(2, result.Tags.Length);
            Assert.Contains("tag1", result.Tags);
            Assert.Contains("tag2", result.Tags);
        }

        [Fact]
        public void ParseResponse_MalformedJson_ThrowsInvalidOperationException()
        {
            // Arrange
            var json = "{ invalid json }";

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => _responseParser.ParseResponse(json));
        }

        [Fact]
        public void ParseResponse_MissingTags_ThrowsInvalidOperationException()
        {
            // Arrange
            var json = @"{ ""otherField"": ""value"" }";

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => _responseParser.ParseResponse(json));
        }

        [Fact]
        public void ParseResponse_EmptyTags_ThrowsInvalidOperationException()
        {
            // Arrange
            var json = @"{ ""tags"": [] }";

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => _responseParser.ParseResponse(json));
        }
    }
}