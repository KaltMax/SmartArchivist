namespace SmartArchivist.Batch.Models
{
    /// <summary>
    /// Represents the configuration settings required for batch processing, including backup, retention, database
    /// connection, and MinIO storage options.
    /// </summary>
    public class BatchConfig
    {
        public string BackupPath { get; set; } = string.Empty;
        public string ConnectionString { get; set; } = string.Empty;
        public string MinIoEndpoint { get; set; } = string.Empty;
        public string MinIoAccessKey { get; set; } = string.Empty;
        public string MinIoSecretKey { get; set; } = string.Empty;
        public string MinIoBucketName { get; set; } = string.Empty;
        public bool MinIoUseSsl { get; set; } = false;
    }
}
