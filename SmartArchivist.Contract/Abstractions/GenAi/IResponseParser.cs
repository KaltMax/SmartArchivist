using SmartArchivist.Contract.DTOs;

namespace SmartArchivist.Contract.Abstractions.GenAi
{
    /// <summary>
    /// Defines a contract for parsing JSON responses from GenAi services.
    /// </summary>
    public interface IResponseParser
    {
        GenAiResult ParseResponse(string jsonResponse);
    }
}
