using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartArchivist.Application.Mapping;
using SmartArchivist.Application.Services;
using SmartArchivist.Contract.Abstractions.Messaging;
using SmartArchivist.Contract.Abstractions.Search;
using SmartArchivist.Contract.Abstractions.Storage;
using SmartArchivist.Contract.Logger;
using SmartArchivist.Dal.Data;
using SmartArchivist.Dal.Repositories;
using SmartArchivist.Infrastructure.ElasticSearch;
using SmartArchivist.Infrastructure.MinIo;
using SmartArchivist.Infrastructure.RabbitMq;
using SmartArchivist.Api.Configuration;
using SmartArchivist.Api.Hubs;

namespace SmartArchivist.Api
{
    public partial class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container
            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            // Add SignalR for real-time notifications
            builder.Services.AddSignalR();

            // Add JWT Authentication for REST API and SignalR
            // 1. Frontend sends POST /api/auth/token
            // 2. Backend responds with JWT token
            // 3. Frontend includes token in Authorization header for REST: "Bearer {token}"
            // 4. Frontend includes token in query string for SignalR: "?access_token={token}"
            // 5. Backend validates token using the secret (frontend never knows the secret)
            builder.Services.ConfigureJwtAuthentication(builder.Configuration);

            // Add CORS policy - origins configured in appsettings.json
            var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                                 ?? ["http://localhost"];

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowFrontend", policy =>
                {
                    policy.WithOrigins(allowedOrigins)
                          .AllowAnyMethod()
                          .AllowAnyHeader()
                          .AllowCredentials();
                });
            });

            // Register logger wrapper
            builder.Services.AddSingleton(typeof(ILoggerWrapper<>), typeof(LoggerWrapper<>));

            // RabbitMQ Setup
            builder.Services.AddOptions<RabbitMqConfig>()
                .Bind(builder.Configuration.GetSection("RabbitMQ"))
                .ValidateDataAnnotations()
                .ValidateOnStart();
            builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<RabbitMqConfig>>().Value);
            builder.Services.AddSingleton<IMessageSerializer, MessageSerializer>();
            builder.Services.AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();
            builder.Services.AddSingleton<IRabbitMqConsumer, RabbitMqConsumer>();

            // MinIO Setup (same pattern as RabbitMQ)
            builder.Services.AddOptions<MinioConfig>()
                .Bind(builder.Configuration.GetSection("MinIO"))
                .ValidateDataAnnotations()
                .ValidateOnStart();
            builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<MinioConfig>>().Value);
            builder.Services.AddSingleton<IFileStorageService, MinioFileStorageService>();

            // ElasticSearch Setup
            builder.Services.AddOptions<ElasticSearchConfig>()
                .Bind(builder.Configuration.GetSection("ElasticSearch"))
                .ValidateDataAnnotations()
                .ValidateOnStart();
            builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<ElasticSearchConfig>>().Value);
            builder.Services.AddSingleton<IIndexingService, ElasticSearchIndexingService>();
            builder.Services.AddSingleton<ISearchService, ElasticSearchService>();

            // Register BL and DAL services
            builder.Services.AddScoped<IDocumentService, DocumentService>();
            builder.Services.AddAutoMapper(cfg =>
            {
                cfg.AddProfile<AutoMapperConfig>();
                cfg.LicenseKey = builder.Configuration["LicenceKeys:AutoMapperLicenceKey"];
            });

            var connStr = Environment.GetEnvironmentVariable("SMARTARCHIVIST_DB_CONNECTION")
                         ?? builder.Configuration.GetConnectionString("SmartArchivistDb");
            if (!string.IsNullOrWhiteSpace(connStr))
            {
                builder.Services.AddDbContext<SmartArchivistDbContext>(opt =>
                    opt.UseNpgsql(connStr));
                builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();
            }

            // Register background workers
            builder.Services.AddHostedService<Workers.DocumentResultWorker>();
            builder.Services.AddHostedService<Workers.FailedDocumentProcessingHandler>();

            var app = builder.Build();

            // Apply pending EF Core migrations at startup
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<SmartArchivistDbContext>();
                db.Database.Migrate();
            }

            // Initialize MinIO bucket
            using (var scope = app.Services.CreateScope())
            {
                var fileStorageService = scope.ServiceProvider.GetRequiredService<IFileStorageService>();
                fileStorageService.InitializeAsync().GetAwaiter().GetResult();
            }

            // Initialize ElasticSearch index
            using (var scope = app.Services.CreateScope())
            {
                var indexingService = scope.ServiceProvider.GetRequiredService<IIndexingService>();
                indexingService.InitializeAsync().GetAwaiter().GetResult();
            }

            app.UseDeveloperExceptionPage();
            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseCors("AllowFrontend");
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();

            // Map SignalR hub for real-time document notifications
            app.MapHub<DocumentHub>("/hubs/documents");

            app.Run();
        }
    }
}