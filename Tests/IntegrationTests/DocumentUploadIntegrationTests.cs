using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SmartArchivist.Contract.Abstractions.Messaging;
using SmartArchivist.Contract.Abstractions.Storage;
using SmartArchivist.Contract.DTOs;
using SmartArchivist.Contract.DTOs.Messages;
using SmartArchivist.Contract.Enums;
using SmartArchivist.Dal.Repositories;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Tests.IntegrationTests.Infrastructure;

namespace Tests.IntegrationTests
{
    [Collection("IntegrationTests")]
    public class DocumentUploadIntegrationTests : IntegrationTestBase
    {
        public DocumentUploadIntegrationTests(IntegrationTestFixture fixture) : base(fixture)
        {
        }

        /// <summary>
        /// Happy Path - Upload PDF document and verify it's persisted to database
        /// Tests: HTTP ? Controller ? Service ? Repository ? PostgreSQL integration
        /// Validates: Document upload, database persistence, state management, message publishing
        /// Uses: Real PDF file with known content about Drumnadrochit
        /// </summary>
        [Fact]
        public async Task UploadDocument_ValidPdf_PersistsToDatabase()
        {
            // Arrange - Load real PDF file
            var pdfBytes = LoadTestPdfFile();
            Assert.True(pdfBytes.Length > 1000);

            var documentName = $"drumnadrochit-test-{Guid.NewGuid()}";
            var fileName = $"{documentName}.pdf";

            // Mock file storage to return a fake path
            using var scope = Factory.Services.CreateScope();
            var mockFileStorage = scope.ServiceProvider.GetRequiredService<IFileStorageService>();
            mockFileStorage.UploadFileAsync(
                Arg.Any<Guid>(),
                Arg.Any<string>(),
                Arg.Any<byte[]>(),
                Arg.Any<string>()
            ).Returns(Task.FromResult($"test-documents/{Guid.NewGuid()}/{fileName}"));

            // Create multipart form data
            using var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(pdfBytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            content.Add(fileContent, "file", fileName);
            content.Add(new StringContent(documentName), "name");

            // Act - Upload document via HTTP POST
            var uploadResponse = await Client.PostAsync("/api/documents/upload", content);

            // Assert - HTTP Response
            Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);
            var uploadedDoc = await uploadResponse.Content.ReadFromJsonAsync<DocumentDto>();
            Assert.NotNull(uploadedDoc);
            Assert.NotEqual(Guid.Empty, uploadedDoc.Id);
            Assert.Equal(documentName, uploadedDoc.Name);
            Assert.Equal(".pdf", uploadedDoc.FileExtension);
            Assert.Equal("application/pdf", uploadedDoc.ContentType);
            Assert.Equal(pdfBytes.Length, uploadedDoc.FileSize);
            Assert.Equal(DocumentState.Uploaded, uploadedDoc.State);
            Assert.True(uploadedDoc.UploadDate <= DateTime.UtcNow);
            Assert.True(uploadedDoc.UploadDate >= DateTime.UtcNow.AddMinutes(-1));

            // Assert - Database Persistence
            using var dbScope = Factory.Services.CreateScope();
            var repository = dbScope.ServiceProvider.GetRequiredService<IDocumentRepository>();
            var doc = await repository.GetByIdAsync(uploadedDoc.Id);

            Assert.NotNull(doc);
            Assert.Equal(DocumentState.Uploaded, doc.State);
            Assert.Equal(documentName, doc.Name);
            Assert.NotNull(doc.FilePath);
            Assert.NotEmpty(doc.FilePath);
            Assert.Equal(".pdf", doc.FileExtension);
            Assert.Equal("application/pdf", doc.ContentType);
            Assert.True(doc.FileSize > 0);

            // Assert - RabbitMQ Publisher Called
            var mockPublisher = scope.ServiceProvider.GetRequiredService<IRabbitMqPublisher>();
            await mockPublisher.Received(1).PublishAsync(
                Arg.Is<DocumentUploadedMessage>(msg =>
                    msg.DocumentId == uploadedDoc.Id &&
                    msg.FileName == fileName &&
                    msg.ContentType == "application/pdf"
                ),
                "smartarchivist.ocr"
            );
        }

        private byte[] LoadTestPdfFile()
        {
            var testFilePath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "IntegrationTests",
                "TestFiles",
                "integration-test-file.pdf"
            );

            if (!File.Exists(testFilePath))
            {
                throw new FileNotFoundException(
                    $"Test PDF file not found at: {testFilePath}. " +
                    "Ensure the file is copied to output directory (set 'Copy to Output Directory' = 'Copy if newer')."
                );
            }

            return File.ReadAllBytes(testFilePath);
        }
    }
}