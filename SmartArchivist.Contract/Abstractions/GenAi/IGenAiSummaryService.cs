using SmartArchivist.Contract.DTOs;

namespace SmartArchivist.Contract.Abstractions.GenAi
{
    /// <summary>
    /// Defines a contract for GenAi services that generate summaries from extracted text.
    /// </summary>
    public interface IGenAiSummaryService
    {
        Task<GenAiResult> GenerateSummaryAsync(string extractedText);
    }
}
