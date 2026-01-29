using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartArchivist.Contract.Abstractions.Messaging;
using SmartArchivist.Contract.Abstractions.Ocr;
using SmartArchivist.Contract.Abstractions.Storage;
using SmartArchivist.Contract.Logger;
using SmartArchivist.Dal.Data;
using SmartArchivist.Dal.Repositories;
using SmartArchivist.Infrastructure.MinIo;
using SmartArchivist.Infrastructure.Ocr;
using SmartArchivist.Infrastructure.RabbitMq;
using SmartArchivist.Ocr.Workers;

namespace SmartArchivist.Ocr
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Register logger wrapper
            builder.Services.AddSingleton(typeof(ILoggerWrapper<>), typeof(LoggerWrapper<>));

            // Configure RabbitMQ
            builder.Services.AddOptions<RabbitMqConfig>()
                .Bind(builder.Configuration.GetSection("RabbitMQ"))
                .ValidateDataAnnotations()
                .ValidateOnStart();
            builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<RabbitMqConfig>>().Value);
            builder.Services.AddSingleton<IMessageSerializer, MessageSerializer>();
            builder.Services.AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();
            builder.Services.AddSingleton<IRabbitMqConsumer, RabbitMqConsumer>();

            // Configure OCR
            builder.Services.AddOptions<OcrConfig>()
                .Bind(builder.Configuration.GetSection("Ocr"))
                .ValidateDataAnnotations()
                .ValidateOnStart();
            builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<OcrConfig>>().Value);

            // Register OCR services
            builder.Services.AddSingleton<IPdfToImageConverter, MagickPdfToImageConverter>();
            builder.Services.AddSingleton<IOcrService, TesseractOcrService>();

            // Configure MinIO service
            builder.Services.AddOptions<MinioConfig>()
                .Bind(builder.Configuration.GetSection("MinIO"))
                .ValidateDataAnnotations()
                .ValidateOnStart();
            builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<MinioConfig>>().Value);
            builder.Services.AddSingleton<IFileStorageService, MinioFileStorageService>();

            // Configure Database
            var connStr = Environment.GetEnvironmentVariable("SMARTARCHIVIST_DB_CONNECTION")
                         ?? builder.Configuration.GetConnectionString("SmartArchivistDb");
            if (!string.IsNullOrWhiteSpace(connStr))
            {
                builder.Services.AddDbContext<SmartArchivistDbContext>(opt =>
                    opt.UseNpgsql(connStr));
                builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();
            }

            // Register worker services
            builder.Services.AddHostedService<OcrWorker>();

            // Add health checks
            builder.Services.AddHealthChecks();

            var app = builder.Build();

            // Apply pending EF Core migrations at startup
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<SmartArchivistDbContext>();
                db.Database.Migrate();
            }

            // Configure HTTP endpoints
            app.MapHealthChecks("/health");
            app.MapGet("/", () => "SmartArchivist.Ocr Worker Service is running");

            app.Run();
        }
    }
}
