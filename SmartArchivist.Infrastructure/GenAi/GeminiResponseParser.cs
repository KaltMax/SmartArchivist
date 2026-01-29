using System.Text.Json;
using SmartArchivist.Contract.DTOs;
using SmartArchivist.Contract.Abstractions.GenAi;
using SmartArchivist.Contract.Logger;

namespace SmartArchivist.Infrastructure.GenAi
{
    /// <summary>
    /// Parses responses from the Gemini API and extracts GenAI results.
    /// </summary>
    public class GeminiResponseParser : IResponseParser
    {
        private readonly ILoggerWrapper<GeminiResponseParser> _logger;

        public GeminiResponseParser(ILoggerWrapper<GeminiResponseParser> logger)
        {
            _logger = logger;
        }

        public GenAiResult ParseResponse(string jsonResponse)
        {
            try
            {
                using var document = JsonDocument.Parse(jsonResponse);
                var root = document.RootElement;

                // Navigate the response structure: candidates[0].content.parts[0].text
                if (root.TryGetProperty("candidates", out var candidates) &&
                    candidates.GetArrayLength() > 0)
                {
                    var firstCandidate = candidates[0];
                    if (firstCandidate.TryGetProperty("content", out var content) &&
                        content.TryGetProperty("parts", out var parts) &&
                        parts.GetArrayLength() > 0)
                    {
                        var firstPart = parts[0];
                        if (firstPart.TryGetProperty("text", out var text))
                        {
                            var textContent = text.GetString()
                                ?? throw new InvalidOperationException("Generated text is null.");

                            var cleanedText = StripMarkdownCodeBlocks(textContent);

                            var genAiResult = JsonSerializer.Deserialize<GenAiResult>(cleanedText);
                            if (genAiResult == null)
                            {
                                throw new InvalidOperationException("Failed to deserialize GenAiResult.");
                            }

                            return genAiResult;
                        }
                    }
                }

                _logger.LogWarning("Unexpected Gemini API response structure: {Response}", jsonResponse);
                throw new InvalidOperationException("Unexpected Gemini API response structure.");
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse Gemini API response: {Response}", jsonResponse);
                throw new InvalidOperationException("Failed to parse Gemini API response", ex);
            }
        }

        private string StripMarkdownCodeBlocks(string text)
        {
            var cleanedText = text.Trim();
            if (cleanedText.StartsWith("```json"))
            {
                cleanedText = cleanedText.Substring(7);
            }
            else if (cleanedText.StartsWith("```"))
            {
                cleanedText = cleanedText.Substring(3);
            }
            if (cleanedText.EndsWith("```"))
            {
                cleanedText = cleanedText.Substring(0, cleanedText.Length - 3);
            }
            return cleanedText.Trim();
        }
    }
}
