namespace SmartArchivist.Batch.Models
{
    /// <summary>
    /// Represents metadata and summary information for a backup operation, including statistics and error details.
    /// </summary>
    public class BackupManifest
    {
        public DateTime BackupDate { get; set; }
        public int TotalDocuments { get; set; }
        public long TotalSizeBytes { get; set; }
        public int SuccessfulBackups { get; set; }
        public int FailedBackups { get; set; }
        public List<string> Errors { get; set; } = new();
    }
}
