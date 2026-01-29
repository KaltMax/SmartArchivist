using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartArchivist.Batch.Models;
using SmartArchivist.Batch.Services;
using SmartArchivist.Contract.Abstractions.Storage;
using SmartArchivist.Contract.Logger;
using SmartArchivist.Dal.Data;
using SmartArchivist.Dal.Repositories;
using SmartArchivist.Infrastructure.MinIo;

namespace SmartArchivist.Batch
{
    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);

            // Load configuration
            builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            var batchConfig = builder.Configuration.GetSection("BatchConfig").Get<BatchConfig>()
                              ?? throw new InvalidOperationException("BatchConfig section is missing in appsettings.json");

            // Register BatchConfig as singleton
            builder.Services.AddSingleton(batchConfig);

            // Configure logging
            builder.Logging.ClearProviders();
            builder.Logging.AddConsole();

            // Register ILoggerWrapper
            builder.Services.AddSingleton(typeof(ILoggerWrapper<>), typeof(LoggerWrapper<>));

            // Register DbContext
            builder.Services.AddDbContext<SmartArchivistDbContext>(options =>
                options.UseNpgsql(batchConfig.ConnectionString));

            // Register Repository
            builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();

            // Register MinIO File Storage
            builder.Services.AddSingleton<IFileStorageService>(sp =>
            {
                var minioConfig = new MinioConfig
                {
                    Endpoint = batchConfig.MinIoEndpoint,
                    AccessKey = batchConfig.MinIoAccessKey,
                    SecretKey = batchConfig.MinIoSecretKey,
                    BucketName = batchConfig.MinIoBucketName,
                    UseSsl = batchConfig.MinIoUseSsl
                };

                var logger = sp.GetRequiredService<ILogger<MinioFileStorageService>>();
                return new MinioFileStorageService(minioConfig, logger);
            });

            // Register Backup Service
            builder.Services.AddScoped<IBackupService, BackupService>();

            // Build host
            var host = builder.Build();

            // Execute backup
            try
            {
                using var scope = host.Services.CreateScope();
                var backupService = scope.ServiceProvider.GetRequiredService<IBackupService>();

                Console.WriteLine("Starting SmartArchivist Backup Job...");
                await backupService.ExecuteBackupAsync();
                Console.WriteLine("Backup completed successfully!");

                return 0; // Success
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Backup failed: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                return 1; // Failure
            }
        }
    }
}
