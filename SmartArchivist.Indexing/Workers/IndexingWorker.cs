using SmartArchivist.Contract;
using SmartArchivist.Contract.Abstractions.Messaging;
using SmartArchivist.Contract.Abstractions.Search;
using SmartArchivist.Contract.DTOs.Messages;
using SmartArchivist.Contract.Enums;
using SmartArchivist.Contract.Logger;
using SmartArchivist.Dal.Repositories;

namespace SmartArchivist.Indexing.Workers
{
    public class IndexingWorker : BackgroundService
    {
        private readonly ILoggerWrapper<IndexingWorker> _logger;
        private readonly IRabbitMqConsumer _consumer;
        private readonly IRabbitMqPublisher _publisher;
        private readonly IIndexingService _indexingService;
        private readonly IServiceProvider _serviceProvider;

        public IndexingWorker(
            ILoggerWrapper<IndexingWorker> logger,
            IRabbitMqConsumer consumer,
            IRabbitMqPublisher publisher,
            IIndexingService indexingService,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _consumer = consumer;
            _publisher = publisher;
            _indexingService = indexingService;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Indexing worker starting...");

            // Subscribe to the IndexingQueue
            _consumer.Subscribe<GenAiCompletedMessage>(
                QueueNames.IndexingQueue,
                HandleGenAiCompletedAsync
            );

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }

        private async Task HandleGenAiCompletedAsync(GenAiCompletedMessage message)
        {
            _logger.LogInformation("Indexing document {DocumentId}", message.DocumentId);

            try
            {
                // 1. Index document in ElasticSearch
                await _indexingService.IndexDocumentAsync(
                    message.DocumentId,
                    message.FileName,
                    message.ExtractedText,
                    message.Summary, message.Tags
                );

                // 2. Update document state to Indexed
                _logger.LogDebug("Updating document {DocumentId} state to Indexed", message.DocumentId);
                using (var scope = _serviceProvider.CreateScope())
                {
                    var documentRepository = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();
                    await documentRepository.UpdateStateAsync(message.DocumentId, DocumentState.Indexed);
                }

                // 3. Publish IndexingCompletedMessage to DocumentResultQueue
                await _publisher.PublishAsync(
                    new IndexingCompletedMessage
                    {
                        DocumentId = message.DocumentId,
                        FileName = message.FileName
                    },
                    QueueNames.DocumentResultQueue
                );

            }
            catch (Exception ex )
            {
                _logger.LogError(ex, "Error indexing document {DocumentId}", message.DocumentId);

                // Mark document as failed
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var documentRepository = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();
                        await documentRepository.UpdateStateAsync(message.DocumentId, DocumentState.Failed);
                    }
                    _logger.LogInformation("Marked document {DocumentId} as Failed", message.DocumentId);
                }
                catch (Exception stateEx)
                {
                    _logger.LogError(stateEx, "Failed to update document {DocumentId} state to Failed", message.DocumentId);
                }

                throw; // Rethrow to trigger message requeue
            }
        }
    }
}
