namespace SmartArchivist.Batch.Services
{
    /// <summary>
    /// Defines a service that performs backup operations asynchronously.
    /// </summary>
    public interface IBackupService
    {
        Task ExecuteBackupAsync();
    }
}
