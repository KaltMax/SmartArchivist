using SmartArchivist.Contract;
using SmartArchivist.Contract.Abstractions.Messaging;
using SmartArchivist.Contract.Abstractions.Ocr;
using SmartArchivist.Contract.Abstractions.Storage;
using SmartArchivist.Contract.DTOs.Messages;
using SmartArchivist.Contract.Enums;
using SmartArchivist.Contract.Logger;
using SmartArchivist.Dal.Repositories;

namespace SmartArchivist.Ocr.Workers
{
    /// <summary>
    /// Provides a background service that listens for document upload messages, performs OCR processing,
    /// saves the extracted text to the database, and publishes the results to a downstream queue.
    /// </summary>
    public class OcrWorker : BackgroundService
    {
        private readonly ILoggerWrapper<OcrWorker> _logger;
        private readonly IRabbitMqConsumer _consumer;
        private readonly IRabbitMqPublisher _publisher;
        private readonly IPdfToImageConverter _pdfToImageConverter;
        private readonly IFileStorageService _fileStorageService;
        private readonly IOcrService _ocrService;
        private readonly IServiceProvider _serviceProvider;

        public OcrWorker(
            ILoggerWrapper<OcrWorker> logger,
            IRabbitMqConsumer consumer,
            IRabbitMqPublisher publisher,
            IFileStorageService fileStorageService,
            IPdfToImageConverter pdfToImageConverter,
            IOcrService ocrService,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _consumer = consumer;
            _publisher = publisher;
            _pdfToImageConverter = pdfToImageConverter;
            _fileStorageService = fileStorageService;
            _ocrService = ocrService;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("OCR Worker starting...");

            // Subscribe to the OCR queue
            _consumer.Subscribe<DocumentUploadedMessage>(
                QueueNames.OcrQueue,
                HandleDocumentUploadedAsync
            );

            _logger.LogInformation("OCR Worker is now listening for messages on queue: {QueueName}", QueueNames.OcrQueue);

            // Keep the worker alive
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }

        private async Task HandleDocumentUploadedAsync(DocumentUploadedMessage message)
        {
            _logger.LogInformation("Processing OCR for document {DocumentId} with filename {FileName}", message.DocumentId, message.FileName);

            try
            {
                // 1. Retrieve document from MinIO
                _logger.LogDebug("Downloading file from storage: {StoragePath}", message.StoragePath);
                await using var pdfStream = await _fileStorageService.DownloadFileAsync(message.StoragePath);

                // 2. Convert PDF pages to images
                _logger.LogDebug("Converting PDF to image for document {DocumentId}", message.DocumentId);
                var images = await _pdfToImageConverter.ConvertToImagesAsync(pdfStream);

                // 3. Perform OCR extraction
                _logger.LogDebug("Performing OCR extraction for document {DocumentId}", message.DocumentId);
                var extractedText = await _ocrService.ExtractTextFromImagesAsync(images);

                // 4. Save OCR text to database and update state
                _logger.LogDebug("Saving OCR text and updating state for document {DocumentId}", message.DocumentId);
                using (var scope = _serviceProvider.CreateScope())
                {
                    var documentRepository = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();
                    await documentRepository.UpdateOcrTextAsync(message.DocumentId, extractedText);
                    await documentRepository.UpdateStateAsync(message.DocumentId, DocumentState.OcrCompleted);
                }

                _logger.LogInformation("Successfully completed OCR and updated state for document {DocumentId}", message.DocumentId);

                // 5. Publish message to GenAI queue
                await _publisher.PublishAsync(
                    new OcrCompletedMessage
                    {
                        DocumentId = message.DocumentId,
                        FileName = message.FileName,
                        ExtractedText = extractedText,
                    },
                    QueueNames.GenAiQueue
                );

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process OCR for document {DocumentId}", message.DocumentId);

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
