using System.Text;
using System.Text.Json;
using SmartArchivist.Contract.Abstractions.GenAi;
using SmartArchivist.Contract.DTOs;
using SmartArchivist.Contract.Logger;

namespace SmartArchivist.Infrastructure.GenAi
{
    /// <summary>
    /// Provides an implementation of the IGenAiSummaryService that generates document summaries using the Google Gemini
    /// generative AI API.
    /// </summary>
    public class GeminiGenAiSummaryService : IGenAiSummaryService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILoggerWrapper<GeminiGenAiSummaryService> _logger;
        private readonly GenAiConfig _config;
        private readonly IRequestBuilder _requestBuilder;
        private readonly IResponseParser _responseParser;

        public GeminiGenAiSummaryService(
            IHttpClientFactory httpClientFactory,
            ILoggerWrapper<GeminiGenAiSummaryService> logger,
            GenAiConfig config,
            IRequestBuilder requestBuilder,
            IResponseParser responseParser)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _config = config;
            _requestBuilder = requestBuilder;
            _responseParser = responseParser;
        }

        public async Task<GenAiResult> GenerateSummaryAsync(string extractedText)
        {
            if (string.IsNullOrWhiteSpace(extractedText))
            {
                _logger.LogWarning("Extracted text is empty or null. Cannot generate summary.");
                throw new ArgumentException("Extracted text cannot be null or empty.", nameof(extractedText));
            }

            _logger.LogInformation("Starting Gemini API request for document summary generation");

            try
            {
                var payload = _requestBuilder.BuildPayload(extractedText, _config.SystemPrompt);

                var json = JsonSerializer.Serialize(payload);
                var body = new StringContent(json, Encoding.UTF8, "application/json");

                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(_config.TimeoutSeconds);
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Add("X-goog-api-key", _config.ApiKey);

                _logger.LogDebug("Sending request to Gemini API at {Url}", _config.ApiUrl);

                var response = await client.PostAsync(_config.ApiUrl, body);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Gemini API request failed with status {StatusCode}: {ErrorContent}",
                        response.StatusCode, errorContent);
                    throw new HttpRequestException($"Gemini API request failed with status {response.StatusCode}: {errorContent}");
                }

                var result = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("Received response from Gemini API: {Response}", result);

                var genAiResult = _responseParser.ParseResponse(result);

                _logger.LogInformation("Successfully generated summary and tags using Gemini API");
                return genAiResult;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error occurred while calling Gemini API");
                throw new InvalidOperationException("Failed to communicate with Gemini API", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred while generating summary");
                throw new InvalidOperationException("Failed to generate summary using Gemini API", ex);
            }
        }
    }
}
