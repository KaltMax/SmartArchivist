using SmartArchivist.Infrastructure.GenAi;
using System.Text.Json;

namespace Tests.SmartArchivist.InfrastructureTests
{
    public class GeminiRequestBuilderTests
    {
        private readonly GeminiRequestBuilder _requestBuilder;

        public GeminiRequestBuilderTests()
        {
            _requestBuilder = new GeminiRequestBuilder();
        }

        [Fact]
        public void BuildPayload_ValidInput_ReturnsCorrectStructure()
        {
            // Arrange
            var documentText = "This is a test document.";
            var systemPrompt = "Summarize this document.";

            // Act
            var payload = _requestBuilder.BuildPayload(documentText, systemPrompt);
            var json = JsonSerializer.Serialize(payload);
            var deserialized = JsonSerializer.Deserialize<JsonElement>(json);

            // Assert - Check systemInstruction contains the system prompt
            Assert.True(deserialized.TryGetProperty("systemInstruction", out var systemInstruction));
            Assert.True(systemInstruction.TryGetProperty("parts", out var systemParts));
            var firstSystemPart = systemParts.EnumerateArray().First();
            Assert.True(firstSystemPart.TryGetProperty("text", out var systemText));
            Assert.Equal(systemPrompt, systemText.GetString());

            // Assert - Check contents contains the document text
            Assert.True(deserialized.TryGetProperty("contents", out var contents));
            Assert.Equal(JsonValueKind.Array, contents.ValueKind);
            var firstContent = contents.EnumerateArray().First();
            Assert.True(firstContent.TryGetProperty("parts", out var parts));
            var firstPart = parts.EnumerateArray().First();
            Assert.True(firstPart.TryGetProperty("text", out var text));
            Assert.Contains(documentText, text.GetString());

            // Assert - Check generationConfig exists
            Assert.True(deserialized.TryGetProperty("generationConfig", out var generationConfig));
            Assert.True(generationConfig.TryGetProperty("response_mime_type", out var mimeType));
            Assert.Equal("application/json", mimeType.GetString());
        }

        [Theory]
        [InlineData("", "prompt")]
        [InlineData("text", "")]
        public void BuildPayload_EmptyInput_StillBuildsPayload(string documentText, string systemPrompt)
        {
            // Act
            var payload = _requestBuilder.BuildPayload(documentText, systemPrompt);

            // Assert
            Assert.NotNull(payload);
        }
    }
}