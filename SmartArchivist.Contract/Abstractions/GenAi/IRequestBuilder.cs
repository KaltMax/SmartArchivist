namespace SmartArchivist.Contract.Abstractions.GenAi
{
    /// <summary>
    /// Defines a contract for building a payload object from extracted text and a system prompt.
    /// </summary>
    public interface IRequestBuilder
    {
        object BuildPayload(string extractedText, string systemPrompt);
    }
}
