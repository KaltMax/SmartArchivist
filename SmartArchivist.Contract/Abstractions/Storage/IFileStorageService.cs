namespace SmartArchivist.Contract.Abstractions.Storage
{
    /// <summary>
    /// Defines a contract for uploading, downloading, deleting, and checking the existence of files in a remote storage
    /// system.
    /// </summary>
    public interface IFileStorageService
    {
        Task InitializeAsync();
        Task<string> UploadFileAsync(Guid documentId, string fileName, byte[] fileContent, string contentType);
        Task<Stream> DownloadFileAsync(string storedPath);
        Task DeleteFileAsync(string storedPath);
    }
}