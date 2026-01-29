using NSubstitute;
using SmartArchivist.Contract.Abstractions.GenAi;
using SmartArchivist.Contract.DTOs;
using SmartArchivist.Contract.Logger;
using SmartArchivist.Infrastructure.GenAi;
using System.Net;

namespace Tests.SmartArchivist.InfrastructureTests
{
    public class GeminiGenAiServiceTests
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILoggerWrapper<GeminiGenAiSummaryService> _logger;
        private readonly GenAiConfig _config;
        private readonly IRequestBuilder _requestBuilder;
        private readonly IResponseParser _responseParser;

        public GeminiGenAiServiceTests()
        {
            _httpClientFactory = Substitute.For<IHttpClientFactory>();
            _logger = Substitute.For<ILoggerWrapper<GeminiGenAiSummaryService>>();
            _requestBuilder = Substitute.For<IRequestBuilder>();
            _responseParser = Substitute.For<IResponseParser>();
            _config = new GenAiConfig
            {
                ApiKey = "test-key",
                ApiUrl = "https://api.gemini.com",
                TimeoutSeconds = 60,
                SystemPrompt = "Generate a summary"
            };
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task GenerateSummary_EmptyText_ThrowsArgumentException(string? input)
        {
            // Arrange
            var service = new GeminiGenAiSummaryService(_httpClientFactory, _logger, _config, _requestBuilder, _responseParser);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => service.GenerateSummaryAsync(input!));
        }

        [Fact]
        public async Task GenerateSummary_ValidResponse_ReturnsGenAiResult()
        {
            // Arrange
            var responseJson = "test-response-json";
            var expectedResult = new GenAiResult
            {
                Summary = "This is a test summary.",
                Tags = new[] { "tag1", "tag2" }
            };

            _requestBuilder.BuildPayload(Arg.Any<string>(), Arg.Any<string>())
                .Returns(new { test = "payload" });

            _responseParser.ParseResponse(responseJson)
                .Returns(expectedResult);

            var mockHttpMessageHandler = new MockHttpMessageHandler(
                new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(responseJson)
                });

            var httpClient = new HttpClient(mockHttpMessageHandler);
            _httpClientFactory.CreateClient().Returns(httpClient);

            var service = new GeminiGenAiSummaryService(_httpClientFactory, _logger, _config, _requestBuilder, _responseParser);

            // Act
            var result = await service.GenerateSummaryAsync("some document text");

            // Assert
            Assert.Equal("This is a test summary.", result.Summary);
            Assert.Equal(2, result.Tags.Length);
            Assert.Contains("tag1", result.Tags);
            Assert.Contains("tag2", result.Tags);
            _requestBuilder.Received(1).BuildPayload("some document text", _config.SystemPrompt);
            _responseParser.Received(1).ParseResponse(responseJson);
        }

        [Fact]
        public async Task GenerateSummary_HttpError_ThrowsInvalidOperationException()
        {
            // Arrange
            _requestBuilder.BuildPayload(Arg.Any<string>(), Arg.Any<string>())
                .Returns(new { test = "payload" });

            var mockHandler = new MockHttpMessageHandler(
                new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.BadRequest,
                    Content = new StringContent("Bad request")
                });

            var httpClient = new HttpClient(mockHandler);
            _httpClientFactory.CreateClient().Returns(httpClient);

            var service = new GeminiGenAiSummaryService(_httpClientFactory, _logger, _config, _requestBuilder, _responseParser);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.GenerateSummaryAsync("test"));
        }

        [Fact]
        public async Task GenerateSummary_ParserThrowsException_PropagatesException()
        {
            // Arrange
            var responseJson = "malformed-response";

            _requestBuilder.BuildPayload(Arg.Any<string>(), Arg.Any<string>())
                .Returns(new { test = "payload" });

            _responseParser.ParseResponse(responseJson)
                .Returns(_ => throw new InvalidOperationException("Failed to parse response"));

            var mockHandler = new MockHttpMessageHandler(
                new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(responseJson)
                });

            _httpClientFactory.CreateClient().Returns(new HttpClient(mockHandler));
            var service = new GeminiGenAiSummaryService(_httpClientFactory, _logger, _config, _requestBuilder, _responseParser);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.GenerateSummaryAsync("text"));
            Assert.Contains("Failed to generate summary", ex.Message);
        }
    }

    public class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public MockHttpMessageHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_response);
        }
    }
}