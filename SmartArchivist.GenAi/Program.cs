using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartArchivist.Contract.Abstractions.GenAi;
using SmartArchivist.Contract.Abstractions.Messaging;
using SmartArchivist.Contract.Logger;
using SmartArchivist.Dal.Data;
using SmartArchivist.Dal.Repositories;
using SmartArchivist.GenAi.Workers;
using SmartArchivist.Infrastructure.GenAi;
using SmartArchivist.Infrastructure.RabbitMq;

namespace SmartArchivist.GenAi
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

            // Configure GenAI
            builder.Services.AddOptions<GenAiConfig>()
                .Bind(builder.Configuration.GetSection("GenAi"))
                .ValidateDataAnnotations()
                .ValidateOnStart();
            builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<GenAiConfig>>().Value);

            // Register HttpClientFactory for external API calls
            builder.Services.AddHttpClient();

            // Register Gemini GenAI services
            builder.Services.AddSingleton<IRequestBuilder, GeminiRequestBuilder>();
            builder.Services.AddSingleton<IResponseParser, GeminiResponseParser>();
            builder.Services.AddSingleton<IGenAiSummaryService, GeminiGenAiSummaryService>();

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
            builder.Services.AddHostedService<GenAiWorker>();

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
            app.MapGet("/", () => "SmartArchivist.GenAi Worker Service is running");

            app.Run();
        }
    }
}