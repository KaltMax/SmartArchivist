using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using SmartArchivist.Contract.Abstractions.Messaging;
using SmartArchivist.Contract.Abstractions.Search;
using SmartArchivist.Contract.Abstractions.Storage;
using SmartArchivist.Dal.Data;
using SmartArchivist.Api;
using Testcontainers.PostgreSql;

namespace Tests.IntegrationTests.Infrastructure
{
    [CollectionDefinition("IntegrationTests", DisableParallelization = true)]
    public class IntegrationTestCollection : ICollectionFixture<IntegrationTestFixture>
    {
    }

    public class IntegrationTestFixture : IAsyncLifetime
    {
        public PostgreSqlContainer PostgresContainer { get; private set; } = null!;
        public WebApplicationFactory<Program> Factory { get; private set; } = null!;

        public async Task InitializeAsync()
        {
            // Start PostgreSQL container only
            PostgresContainer = new PostgreSqlBuilder()
                .WithImage("postgres:15-alpine")
                .WithDatabase("smartarchivist_test")
                .WithUsername("postgres")
                .WithPassword("test_password")
                .Build();

            await PostgresContainer.StartAsync();

            var postgresConnectionString = PostgresContainer.GetConnectionString();

            Console.WriteLine("=== Integration Test Configuration ===");
            Console.WriteLine($"PostgreSQL: {postgresConnectionString}");
            Console.WriteLine("MinIO: MOCKED (IFileStorageService)");
            Console.WriteLine("RabbitMQ: MOCKED (IRabbitMqPublisher/Consumer)");
            Console.WriteLine("Elasticsearch: MOCKED (IIndexingService/ISearchService)");
            Console.WriteLine("SignalR: MOCKED (IHubContext)");
            Console.WriteLine("=======================================");

            Factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.UseEnvironment("IntegrationTest");

                    builder.ConfigureAppConfiguration(config =>
                    {
                        config.AddInMemoryCollection(new Dictionary<string, string?>
                        {
                            ["ConnectionStrings:SmartArchivistDb"] = postgresConnectionString,
                            // Required config values (not used, but needed for validation)
                            ["RabbitMQ:HostName"] = "localhost",
                            ["RabbitMQ:Port"] = "5672",
                            ["RabbitMQ:UserName"] = "guest",
                            ["RabbitMQ:Password"] = "guest",
                            ["RabbitMQ:VirtualHost"] = "/",
                            ["MinIO:Endpoint"] = "localhost:9000",
                            ["MinIO:AccessKey"] = "minioadmin",
                            ["MinIO:SecretKey"] = "minioadmin",
                            ["MinIO:BucketName"] = "test-bucket",
                            ["MinIO:UseSsl"] = "false",
                            ["ElasticSearch:Url"] = "http://localhost:9200",
                            ["ElasticSearch:IndexName"] = "test-index",
                            ["ElasticSearch:MaxSearchResults"] = "100"
                        });
                    });

                    builder.ConfigureServices(services =>
                    {
                        // Replace DbContext with test container connection
                        var dbContextDescriptor = services.SingleOrDefault(
                            d => d.ServiceType == typeof(DbContextOptions<SmartArchivistDbContext>));
                        if (dbContextDescriptor != null)
                        {
                            services.Remove(dbContextDescriptor);
                        }
                        services.AddDbContext<SmartArchivistDbContext>(options =>
                        {
                            options.UseNpgsql(postgresConnectionString);
                        });

                        // Mock external services with proper initialization handling
                        var mockFileStorage = Substitute.For<IFileStorageService>();
                        mockFileStorage.InitializeAsync().Returns(Task.CompletedTask);
                        services.Replace(ServiceDescriptor.Singleton(mockFileStorage));

                        var mockIndexingService = Substitute.For<IIndexingService>();
                        mockIndexingService.InitializeAsync().Returns(Task.CompletedTask);
                        services.Replace(ServiceDescriptor.Singleton(mockIndexingService));

                        services.Replace(ServiceDescriptor.Singleton(Substitute.For<ISearchService>()));
                        services.Replace(ServiceDescriptor.Singleton(Substitute.For<IRabbitMqPublisher>()));
                        services.Replace(ServiceDescriptor.Singleton(Substitute.For<IRabbitMqConsumer>()));

                        // Remove background workers
                        var workersToRemove = services
                            .Where(s => s.ImplementationType?.Namespace?.Contains("Workers") == true)
                            .ToList();
                        foreach (var worker in workersToRemove)
                        {
                            services.Remove(worker);
                        }

                        // Remove JWT authentication services to avoid conflicts
                        var jwtAuthDescriptors = services
                            .Where(d => d.ServiceType.FullName?.Contains("JwtBearer") == true)
                            .ToList();
                        foreach (var descriptor in jwtAuthDescriptors)
                        {
                            services.Remove(descriptor);
                        }

                        // Add test authentication (will override JWT)
                        services.AddAuthentication("Test")
                            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });

                        services.AddAuthorization(options =>
                        {
                            options.DefaultPolicy = new AuthorizationPolicyBuilder("Test")
                                .RequireAuthenticatedUser()
                                .Build();
                        });
                    });
                });

            // Apply EF Core migrations (use real migrations to catch migration issues in tests)
            using var scope = Factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<SmartArchivistDbContext>();
            await dbContext.Database.MigrateAsync();
        }

        public async Task DisposeAsync()
        {
            Console.WriteLine("=== Cleaning up Integration Test Resources ===");

            try
            {
                await Factory.DisposeAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error disposing Factory: {ex.Message}");
            }

            try
            {
                await PostgresContainer.DisposeAsync();
                Console.WriteLine("PostgreSQL container stopped successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error stopping PostgreSQL container: {ex.Message}");
            }
        }
    }

    public abstract class IntegrationTestBase : IClassFixture<IntegrationTestFixture>
    {
        protected readonly IntegrationTestFixture Fixture;
        protected readonly WebApplicationFactory<Program> Factory;
        protected readonly HttpClient Client;

        protected IntegrationTestBase(IntegrationTestFixture fixture)
        {
            Fixture = fixture;
            Factory = fixture.Factory;
            Client = Factory.CreateClient();
        }
    }
}