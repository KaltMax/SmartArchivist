using SmartArchivist.Contract;
using SmartArchivist.Contract.Abstractions.GenAi;
using SmartArchivist.Contract.Abstractions.Messaging;
using SmartArchivist.Contract.DTOs.Messages;
using SmartArchivist.Contract.Enums;
using SmartArchivist.Contract.Logger;
using SmartArchivist.Dal.Repositories;

namespace SmartArchivist.GenAi.Workers
{
    /// <summary>
    /// Provides a background service that listens for OCR completion messages, generates AI-based summaries,
    /// saves them to the database, and publishes the results to a message queue for indexing.
    /// </summary>
    public class GenAiWorker : BackgroundService
    {
        private readonly ILoggerWrapper<GenAiWorker> _logger;
        private readonly IRabbitMqConsumer _consumer;
        private readonly IRabbitMqPublisher _publisher;
        private readonly IGenAiSummaryService _genAiSummaryService;
        private readonly IServiceProvider _serviceProvider;

        public GenAiWorker(
            ILoggerWrapper<GenAiWorker> logger,
            IRabbitMqConsumer consumer,
            IRabbitMqPublisher publisher,
            IGenAiSummaryService genAiSummaryService,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _consumer = consumer;
            _publisher = publisher;
            _genAiSummaryService = genAiSummaryService;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("GenAi Worker starting...");

            // Subscribe to the GenAi queue
            _consumer.Subscribe<OcrCompletedMessage>(
                QueueNames.GenAiQueue,
                HandleOcrCompletedAsync
            );

            _logger.LogInformation("GenAi worker is now listening for messages on queue: {QueueName}", QueueNames.GenAiQueue);

            // Keep the worker alive
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }

        private async Task HandleOcrCompletedAsync(OcrCompletedMessage message)
        {
            _logger.LogInformation("Generating summary for document {DocumentId}", message.DocumentId);

            try
            {
                // 1. Generate AI summary + tags
                var genAiResult = await _genAiSummaryService.GenerateSummaryAsync(message.ExtractedText);

                // 2. Save GenAI summary + tags to database and update state
                _logger.LogDebug("Saving GenAI summary and updating state for document {DocumentId}", message.DocumentId);
                using (var scope = _serviceProvider.CreateScope())
                {
                    var documentRepository = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();
                    await documentRepository.UpdateGenAiResultAsync(message.DocumentId, genAiResult);
                    await documentRepository.UpdateStateAsync(message.DocumentId, DocumentState.GenAiCompleted);
                }

                _logger.LogInformation("Successfully generated summary and updated state for document {DocumentId}", message.DocumentId);

                // 3. Publish message to DocumentResult queue
                await _publisher.PublishAsync(
                    new GenAiCompletedMessage
                    {
                        DocumentId = message.DocumentId,
                        FileName = message.FileName,
                        Summary = genAiResult.Summary,
                        Tags = genAiResult.Tags,
                        ExtractedText = message.ExtractedText,
                    },
                    QueueNames.IndexingQueue
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate summary for document {DocumentId}", message.DocumentId);

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
