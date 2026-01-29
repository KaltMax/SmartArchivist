using SmartArchivist.Contract.Abstractions.GenAi;

namespace SmartArchivist.Infrastructure.GenAi
{
    public class GeminiRequestBuilder : IRequestBuilder
    {
        /// <summary>
        /// Builds a request payload for the Gemini API with system instruction, content, and JSON schema.
        /// </summary>
        public object BuildPayload(string extractedText, string systemPrompt)
        {
            return new
            {
                systemInstruction = new
                {
                    parts = new[]
                    {
                        new { text = systemPrompt }
                    }
                },
                contents = new[]
                {
                    new
                    {
                        parts = new[] { new { text = $"DOCUMENT CONTENT: {extractedText}" } }
                    }
                },
                generationConfig = new
                {
                    response_mime_type = "application/json",
                    response_schema = new
                    {
                        type = "object",
                        properties = new
                        {
                            summary = new
                            {
                                type = "string",
                                description = "A 2-3 sentence summary of the document"
                            },
                            tags = new
                            {
                                type = "array",
                                description = "3-5 relevant keywords or categories",
                                items = new
                                {
                                    type = "string"
                                }
                            }
                        },
                        required = new[] { "summary", "tags" }
                    }
                }
            };
        }
    }
}
