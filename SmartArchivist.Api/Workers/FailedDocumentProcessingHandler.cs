using Microsoft.AspNetCore.SignalR;
using SmartArchivist.Application.Services;
using SmartArchivist.Contract;
using SmartArchivist.Contract.Abstractions.Messaging;
using SmartArchivist.Contract.DTOs.Messages;
using SmartArchivist.Contract.Logger;
using SmartArchivist.Api.Hubs;

namespace SmartArchivist.Api.Workers
{
    /// <summary>
    /// Background service that listens for failed messages in dead letter queues and performs
    /// rollback by deleting documents that exceeded retry limits.
    /// </summary>
    public class FailedDocumentProcessingHandler : BackgroundService
    {
        private readonly ILoggerWrapper<FailedDocumentProcessingHandler> _logger;
        private readonly IRabbitMqConsumer _consumer;
        private readonly IServiceProvider _serviceProvider;
        private readonly IHubContext<DocumentHub> _hubContext;

        public FailedDocumentProcessingHandler(
            ILoggerWrapper<FailedDocumentProcessingHandler> logger,
            IRabbitMqConsumer consumer,
            IServiceProvider serviceProvider,
            IHubContext<DocumentHub> hubContext)
        {
            _logger = logger;
            _consumer = consumer;
            _serviceProvider = serviceProvider;
            _hubContext = hubContext;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("FailedDocumentProcessing Handler starting...");

            // Subscribe to all dead letter queues
            _consumer.Subscribe<DocumentUploadedMessage>(
                $"{QueueNames.OcrQueue}.dlq",
                async (message) =>
                {
                    _logger.LogInformation("Received failed OCR message for DocumentId: {DocumentId}", message.DocumentId);
                    await HandleFailure(message.DocumentId, message.FileName, "OCR", stoppingToken);
                });

            _consumer.Subscribe<OcrCompletedMessage>(
                $"{QueueNames.GenAiQueue}.dlq",
                async (message) =>
                {
                    _logger.LogInformation("Received failed GenAI message for DocumentId: {DocumentId}", message.DocumentId);
                    await HandleFailure(message.DocumentId, message.FileName, "GenAI", stoppingToken);
                });

            _consumer.Subscribe<GenAiCompletedMessage>(
                $"{QueueNames.DocumentResultQueue}.dlq",
                async (message) =>
                {
                    _logger.LogInformation("Received failed DocumentUpdate message for DocumentId: {DocumentId}", message.DocumentId);
                    await HandleFailure(message.DocumentId, message.FileName, "DocumentUpdate", stoppingToken);
                });

            _consumer.Subscribe<IndexingCompletedMessage>(
                $"{QueueNames.IndexingQueue}.dlq",
                async (message) =>
                {
                    _logger.LogInformation("Received failed Indexing message for DocumentId: {DocumentId}", message.DocumentId);
                    await HandleFailure(message.DocumentId, message.FileName, "Indexing", stoppingToken);
                });

            _logger.LogInformation("FailedDocumentProcessing Handler is now listening for messages on dead letter queues: {OcrDlq}, {GenAiDlq}, {UpdateDlq}",
                $"{QueueNames.OcrQueue}.dlq",
                $"{QueueNames.GenAiQueue}.dlq",
                $"{QueueNames.DocumentResultQueue}.dlq");

            // Keep the worker alive
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }

        private async Task HandleFailure(Guid documentId, string fileName, string stage, CancellationToken stoppingToken)
        {
            _logger.LogWarning("Processing rollback for document {DocumentId} (FileName: {FileName}) that failed at stage: {Stage}",
                documentId, fileName, stage);

            // Create a scope to get scoped services
            using var scope = _serviceProvider.CreateScope();
            var documentService = scope.ServiceProvider.GetRequiredService<IDocumentService>();

            try
            {
                // Delete from MinIO and Postgres
                await documentService.DeleteDocumentAsync(documentId);
                _logger.LogInformation("Successfully deleted document {DocumentId} after max retries exceeded at stage: {Stage}",
                    documentId, stage);

                // Notify WebUI clients via SignalR
                await _hubContext.Clients
                    .Group(documentId.ToString())
                    .SendAsync("DocumentProcessingFailed", new
                    {
                        documentId,
                        fileName,
                        status = "Failed",
                        error = $"Processing failed after 3 attempts at {stage} stage. Document has been removed.",
                        stage,
                        retriesExceeded = true
                    }, stoppingToken);

                _logger.LogInformation("Notified WebUI clients about document {DocumentId} failure and removal", documentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling rollback for document {DocumentId} at stage {Stage}", documentId, stage);

                // Notify UI about rollback failure
                await _hubContext.Clients
                    .Group(documentId.ToString())
                    .SendAsync("DocumentProcessingFailed", new
                    {
                        documentId,
                        fileName,
                        status = "Failed",
                        error = $"Processing failed and automatic cleanup also failed: {ex.Message}",
                        stage
                    }, stoppingToken);
            }
        }
    }
}
