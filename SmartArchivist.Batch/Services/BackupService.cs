using Microsoft.Extensions.Logging;
using SmartArchivist.Batch.Models;
using SmartArchivist.Contract.Abstractions.Storage;
using SmartArchivist.Dal.Repositories;
using System.Text.Json;

namespace SmartArchivist.Batch.Services
{
    /// <summary>
    /// Implements backup operations for documents and their files.
    /// </summary>
    public class BackupService : IBackupService
    {
        private readonly IFileStorageService _fileStorageService;
        private readonly IDocumentRepository _documentRepository;
        private readonly BatchConfig _config;
        private readonly ILogger<BackupService> _logger;
        
        public BackupService(
            IFileStorageService fileStorageService,
            IDocumentRepository documentRepository,
            BatchConfig config,
            ILogger<BackupService> logger)
        {
            _fileStorageService = fileStorageService;
            _documentRepository = documentRepository;
            _config = config;
            _logger = logger;
        }

        public async Task ExecuteBackupAsync()
        {
            _logger.LogInformation("Starting backup process...");

            var backupTimeStamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var backupDirectory = Path.Combine(_config.BackupPath, $"backup_{backupTimeStamp}");
            var filesDirectory = Path.Combine(backupDirectory, "files");

            var manifest = new BackupManifest
            {
                BackupDate = DateTime.UtcNow
            };

            try
            {
                // Create backup directories
                Directory.CreateDirectory(backupDirectory);
                Directory.CreateDirectory(filesDirectory);
                _logger.LogInformation("Created backup directory {BackupDirectory}", backupDirectory);

                // Retrieve all documents from postgres db
                _logger.LogInformation("Retrieving all documents metadata");
                var documents = await _documentRepository.GetAllWithContentAndSummaryAsync();
                var documentList = documents.ToList();
                manifest.TotalDocuments = documentList.Count;
                _logger.LogInformation("Retrieved {Count}", manifest.TotalDocuments);

                // Export metadata to JSON
                var metadataPath = Path.Combine(backupDirectory, "documents-metadata.json");
                var jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                var jsonContent = JsonSerializer.Serialize(documentList, jsonOptions);
                await File.WriteAllTextAsync(metadataPath, jsonContent);
                _logger.LogInformation("Exported metadata to {MetadataPath}", metadataPath);

                // Download files from storage
                long totalSize = 0;

                foreach (var doc in documentList)
                {
                    try
                    {
                        _logger.LogDebug("Downloading file for document {DocumentId}", doc.Id);

                        var fileStream = await _fileStorageService.DownloadFileAsync(doc.FilePath);
                        var fileName = $"{doc.Name}{doc.FileExtension}";
                        var filePath = Path.Combine(filesDirectory, $"{doc.Id}_{fileName}");

                        await using (var fileOutput = File.Create(filePath))
                        {
                            await fileStream.CopyToAsync(fileOutput);
                        }

                        totalSize += doc.FileSize;
                        manifest.SuccessfulBackups++;
                        _logger.LogDebug("Successfully backed up {FileName}", doc.Name);
                    }
                    catch (Exception ex)
                    {
                        manifest.FailedBackups++;
                        var errorMessage = $"Failed to backup document {doc.Id} - {doc.Name} : {ex.Message}";
                        manifest.Errors.Add(errorMessage);
                        _logger.LogError(ex, "Failed to backup {DocumentId}", doc.Id);
                    }
                }

                manifest.TotalSizeBytes = totalSize;

                // Write manifest file
                var manifestPath = Path.Combine(backupDirectory, "backup-manifest.json");
                var manifestJson = JsonSerializer.Serialize(manifest, jsonOptions);
                await File.WriteAllTextAsync(manifestPath, manifestJson);
                _logger.LogInformation("Backup manifest written to {ManifestPath}", manifestPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Backup process failed.");
                throw;
            }
        }
    }
}
