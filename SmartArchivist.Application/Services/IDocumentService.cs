using SmartArchivist.Application.DomainModels;
using SmartArchivist.Contract.Enums;

namespace SmartArchivist.Application.Services
{
    /// <summary>
    /// Defines a contract for document management operations in the business logic layer.
    /// </summary>
    public interface IDocumentService
    {
        Task<DocumentDomain> CreateDocumentAsync(DocumentDomain documentDto, byte[] fileContent);
        Task<IEnumerable<DocumentDomain>> GetAllDocumentsAsync();
        Task<DocumentDomain?> GetDocumentByIdAsync(Guid id);
        Task<Stream> GetDocumentFileAsync(Guid id);
        Task<bool> UpdateDocumentStateAsync(Guid id, DocumentState state);
        Task<DocumentDomain?> UpdateDocumentMetadataAsync(Guid id, string? name, string? summary);
        Task<bool> DeleteDocumentAsync(Guid id);
        Task<IEnumerable<DocumentDomain>> SearchDocumentsAsync(string query);
    }
}
