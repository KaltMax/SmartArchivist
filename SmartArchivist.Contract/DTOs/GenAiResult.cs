using System.Text.Json.Serialization;

namespace SmartArchivist.Contract.DTOs
{
    /// <summary>
    /// Represents the result of the GenAi document processing containing summary and tags.
    /// </summary>
    public class GenAiResult
    {
        [JsonPropertyName("summary")]
        public string Summary { get; set; } = string.Empty;

        [JsonPropertyName("tags")]
        public string[] Tags { get; set; } = Array.Empty<string>();
    }
}
