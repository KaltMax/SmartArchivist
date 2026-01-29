using AutoMapper;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SmartArchivist.Application.DomainModels;
using SmartArchivist.Application.Exceptions;
using SmartArchivist.Application.Services;
using SmartArchivist.Contract;
using SmartArchivist.Contract.Abstractions.Messaging;
using SmartArchivist.Contract.Abstractions.Search;
using SmartArchivist.Contract.Abstractions.Storage;
using SmartArchivist.Contract.DTOs.Messages;
using SmartArchivist.Contract.Logger;
using SmartArchivist.Dal.Entities;
using SmartArchivist.Dal.Exceptions;
using SmartArchivist.Dal.Repositories;

namespace Tests.SmartArchivist.ApplicationTests
{
    public class DocumentServiceTests
    {
        private readonly IMapper _mapper;
        private readonly IDocumentRepository _repository;
        private readonly ILoggerWrapper<DocumentService> _logger;
        private readonly IRabbitMqPublisher _publisher;
        private readonly IFileStorageService _fileStorage;
        private readonly IIndexingService _indexingService;
        private readonly ISearchService _searchService;
        private readonly DocumentService _sut;

        public DocumentServiceTests()
        {
            _mapper = Substitute.For<IMapper>();
            _repository = Substitute.For<IDocumentRepository>();
            _logger = Substitute.For<ILoggerWrapper<DocumentService>>();
            _publisher = Substitute.For<IRabbitMqPublisher>();
            _fileStorage = Substitute.For<IFileStorageService>();
            _indexingService = Substitute.For<IIndexingService>();
            _searchService = Substitute.For<ISearchService>();
            _sut = new DocumentService(_mapper, _repository, _logger, _publisher, _fileStorage, _indexingService, _searchService);
        }

        #region CreateDocumentAsync Tests

        [Fact]
        public async Task CreateDocument_Success_ReturnsDocumentDomain()
        {
            // Arrange
            var inputDto = new DocumentDomain { Name = "test", FileExtension = ".pdf", ContentType = "application/pdf" };
            var fileContent = new byte[] { 1, 2, 3 };
            var documentId = Guid.NewGuid();
            var storedPath = $"{documentId}/test.pdf";

            var mappedDomain = new DocumentDomain { Name = "test", FileExtension = ".pdf", ContentType = "application/pdf" };
            _mapper.Map<DocumentDomain>(inputDto).Returns(mappedDomain);

            _fileStorage.UploadFileAsync(Arg.Any<Guid>(), "test.pdf", fileContent, "application/pdf")
                .Returns(storedPath);

            var savedEntity = new DocumentEntity { Id = documentId, Name = "test", FilePath = storedPath };
            _repository.AddAsync(Arg.Any<DocumentEntity>()).Returns(savedEntity);

            var resultDomain = new DocumentDomain { Id = documentId, Name = "test", FilePath = storedPath };
            _mapper.Map<DocumentDomain>(savedEntity).Returns(resultDomain);

            // Act
            var result = await _sut.CreateDocumentAsync(inputDto, fileContent);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(documentId, result.Id);
            await _fileStorage.Received(1).UploadFileAsync(Arg.Any<Guid>(), "test.pdf", fileContent, "application/pdf");
            await _repository.Received(1).AddAsync(Arg.Any<DocumentEntity>());
            await _publisher.Received(1).PublishAsync(Arg.Any<DocumentUploadedMessage>(), QueueNames.OcrQueue);
        }

        [Fact]
        public async Task CreateDocument_FileStorageFails_ThrowsFileOperationException()
        {
            // Arrange
            var inputDto = new DocumentDomain { Name = "test", FileExtension = ".pdf" };
            var fileContent = new byte[] { 1, 2, 3 };

            _mapper.Map<DocumentDomain>(inputDto).Returns(new DocumentDomain { Name = "test", FileExtension = ".pdf" });

            _fileStorage.UploadFileAsync(Arg.Any<Guid>(), Arg.Any<string>(), fileContent, Arg.Any<string>())
                .Throws(new Exception("MinIO error"));

            // Act & Assert
            await Assert.ThrowsAsync<FileOperationException>(() => _sut.CreateDocumentAsync(inputDto, fileContent));
            await _repository.DidNotReceive().AddAsync(Arg.Any<DocumentEntity>());
            await _publisher.DidNotReceive().PublishAsync(Arg.Any<DocumentUploadedMessage>(), Arg.Any<string>());
        }

        [Fact]
        public async Task CreateDocument_DatabaseFails_PerformsRollback()
        {
            // Arrange
            var inputDto = new DocumentDomain { Name = "test", FileExtension = ".pdf", ContentType = "application/pdf" };
            var fileContent = new byte[] { 1, 2, 3 };
            var storedPath = "guid/test.pdf";

            _mapper.Map<DocumentDomain>(inputDto).Returns(new DocumentDomain { Name = "test", FileExtension = ".pdf", ContentType = "application/pdf" });

            _fileStorage.UploadFileAsync(Arg.Any<Guid>(), "test.pdf", fileContent, "application/pdf")
                .Returns(storedPath);

            _repository.AddAsync(Arg.Any<DocumentEntity>()).Throws(new DocumentRepositoryException("DB error"));

            // Act & Assert
            await Assert.ThrowsAsync<DocumentProcessingException>(() => _sut.CreateDocumentAsync(inputDto, fileContent));
            await _fileStorage.Received(1).DeleteFileAsync(storedPath);
            await _publisher.DidNotReceive().PublishAsync(Arg.Any<DocumentUploadedMessage>(), Arg.Any<string>());
        }

        [Fact]
        public async Task CreateDocument_RollbackFails_LogsWarning()
        {
            // Arrange
            var inputDto = new DocumentDomain { Name = "test", FileExtension = ".pdf", ContentType = "application/pdf" };
            var fileContent = new byte[] { 1, 2, 3 };
            var storedPath = "guid/test.pdf";

            _mapper.Map<DocumentDomain>(inputDto).Returns(new DocumentDomain { Name = "test", FileExtension = ".pdf", ContentType = "application/pdf" });

            _fileStorage.UploadFileAsync(Arg.Any<Guid>(), "test.pdf", fileContent, "application/pdf")
                .Returns(storedPath);

            _repository.AddAsync(Arg.Any<DocumentEntity>()).Throws(new DocumentRepositoryException("DB error"));
            _fileStorage.DeleteFileAsync(storedPath).Throws(new Exception("Delete failed"));

            // Act & Assert
            await Assert.ThrowsAsync<DocumentProcessingException>(() => _sut.CreateDocumentAsync(inputDto, fileContent));
            _logger.Received().LogWarning(Arg.Any<Exception>(), "Failed to cleanup file: {StoredPath}", storedPath);
        }

        [Fact]
        public async Task CreateDocument_DuplicateName_ThrowsDuplicateDocumentException()
        {
            // Arrange
            var inputDto = new DocumentDomain { Name = "duplicate", FileExtension = ".pdf", ContentType = "application/pdf" };
            var fileContent = new byte[] { 1, 2, 3 };
            var storedPath = "guid/duplicate.pdf";

            _mapper.Map<DocumentDomain>(inputDto).Returns(new DocumentDomain { Name = "duplicate", FileExtension = ".pdf", ContentType = "application/pdf" });

            _fileStorage.UploadFileAsync(Arg.Any<Guid>(), "duplicate.pdf", fileContent, "application/pdf")
                .Returns(storedPath);

            _repository.AddAsync(Arg.Any<DocumentEntity>()).Throws(new DocumentAlreadyExistsException("Duplicate"));

            // Act & Assert
            await Assert.ThrowsAsync<DuplicateDocumentException>(() => _sut.CreateDocumentAsync(inputDto, fileContent));
            await _fileStorage.DidNotReceive().DeleteFileAsync(Arg.Any<string>());
        }

        [Fact]
        public async Task CreateDocument_SetsCorrectMetadata()
        {
            // Arrange
            var inputDto = new DocumentDomain { Name = "test", FileExtension = ".pdf", ContentType = "application/pdf" };
            var fileContent = new byte[] { 1, 2, 3, 4, 5 };
            var beforeUpload = DateTime.UtcNow;

            DocumentEntity? capturedEntity = null;

            _mapper.Map<DocumentDomain>(inputDto).Returns(new DocumentDomain { Name = "test", FileExtension = ".pdf", ContentType = "application/pdf" });

            _mapper.Map<DocumentEntity>(Arg.Any<DocumentDomain>())
                .Returns(ci => new DocumentEntity
                {
                    Id = ci.Arg<DocumentDomain>().Id,
                    Name = ci.Arg<DocumentDomain>().Name,
                    FileExtension = ci.Arg<DocumentDomain>().FileExtension,
                    FileSize = ci.Arg<DocumentDomain>().FileSize,
                    UploadDate = ci.Arg<DocumentDomain>().UploadDate,
                    FilePath = ci.Arg<DocumentDomain>().FilePath
                });

            _fileStorage.UploadFileAsync(Arg.Any<Guid>(), "test.pdf", fileContent, Arg.Any<string>())
                .Returns("path/test.pdf");

            _repository.AddAsync(Arg.Do<DocumentEntity>(e => capturedEntity = e))
                .Returns(ci => ci.Arg<DocumentEntity>());

            _mapper.Map<DocumentDomain>(Arg.Any<DocumentEntity>()).Returns(new DocumentDomain());

            // Act
            await _sut.CreateDocumentAsync(inputDto, fileContent);

            // Assert
            Assert.NotNull(capturedEntity);
            Assert.NotEqual(Guid.Empty, capturedEntity.Id);
            Assert.Equal(5, capturedEntity.FileSize);
            Assert.True(capturedEntity.UploadDate >= beforeUpload && capturedEntity.UploadDate <= DateTime.UtcNow.AddSeconds(1));
        }

        [Fact]
        public async Task CreateDocument_CallsMapperTwice()
        {
            // Arrange
            var inputDto = new DocumentDomain { Name = "test", FileExtension = ".pdf", ContentType = "application/pdf" };
            var fileContent = new byte[] { 1, 2, 3 };

            _mapper.Map<DocumentDomain>(inputDto).Returns(new DocumentDomain { Name = "test", FileExtension = ".pdf", ContentType = "application/pdf" });

            _fileStorage.UploadFileAsync(Arg.Any<Guid>(), "test.pdf", fileContent, Arg.Any<string>())
                .Returns("path");

            var savedEntity = new DocumentEntity { Id = Guid.NewGuid(), Name = "test" };
            _repository.AddAsync(Arg.Any<DocumentEntity>()).Returns(savedEntity);
            _mapper.Map<DocumentDomain>(savedEntity).Returns(new DocumentDomain());

            // Act
            await _sut.CreateDocumentAsync(inputDto, fileContent);

            // Assert
            _mapper.Received(1).Map<DocumentDomain>(inputDto);
            _mapper.Received(1).Map<DocumentEntity>(Arg.Any<DocumentDomain>());
            _mapper.Received(1).Map<DocumentDomain>(savedEntity);
        }

        [Fact]
        public async Task CreateDocument_StoresStoredPathInDatabase()
        {
            // Arrange
            var inputDto = new DocumentDomain { Name = "test", FileExtension = ".pdf", ContentType = "application/pdf" };
            var fileContent = new byte[] { 1, 2, 3 };
            var expectedPath = "some-guid/test.pdf";

            DocumentEntity? capturedEntity = null;

            _mapper.Map<DocumentDomain>(inputDto).Returns(new DocumentDomain { Name = "test", FileExtension = ".pdf", ContentType = "application/pdf" });

            _mapper.Map<DocumentEntity>(Arg.Any<DocumentDomain>())
                .Returns(ci => new DocumentEntity
                {
                    Id = ci.Arg<DocumentDomain>().Id,
                    Name = ci.Arg<DocumentDomain>().Name,
                    FilePath = ci.Arg<DocumentDomain>().FilePath
                });

            _fileStorage.UploadFileAsync(Arg.Any<Guid>(), "test.pdf", fileContent, Arg.Any<string>())
                .Returns(expectedPath);

            _repository.AddAsync(Arg.Do<DocumentEntity>(e => capturedEntity = e))
                .Returns(ci => ci.Arg<DocumentEntity>());

            _mapper.Map<DocumentDomain>(Arg.Any<DocumentEntity>()).Returns(new DocumentDomain());

            // Act
            await _sut.CreateDocumentAsync(inputDto, fileContent);

            // Assert
            Assert.NotNull(capturedEntity);
            Assert.Equal(expectedPath, capturedEntity.FilePath);
        }

        [Fact]
        public async Task CreateDocument_LogsDebugAndInfo()
        {
            // Arrange
            var inputDto = new DocumentDomain { Name = "test", FileExtension = ".pdf", ContentType = "application/pdf" };
            var fileContent = new byte[] { 1, 2, 3 };

            _mapper.Map<DocumentDomain>(inputDto).Returns(new DocumentDomain { Name = "test", FileExtension = ".pdf", ContentType = "application/pdf" });

            _fileStorage.UploadFileAsync(Arg.Any<Guid>(), "test.pdf", fileContent, Arg.Any<string>())
                .Returns("path");

            var savedEntity = new DocumentEntity { Id = Guid.NewGuid(), Name = "test" };
            _repository.AddAsync(Arg.Any<DocumentEntity>()).Returns(savedEntity);
            _mapper.Map<DocumentDomain>(savedEntity).Returns(new DocumentDomain { Id = savedEntity.Id });

            // Act
            await _sut.CreateDocumentAsync(inputDto, fileContent);

            // Assert
            _logger.Received().LogDebug("Starting document creation for {DocumentName}", "test");
            _logger.Received().LogInformation("Document created successfully with ID {DocumentId}", savedEntity.Id);
        }

        #endregion

        #region GetAllDocumentsAsync Tests

        [Fact]
        public async Task GetAllDocuments_Success_ReturnsMappedDocuments()
        {
            // Arrange
            var entities = new List<DocumentEntity>
            {
                new() { Id = Guid.NewGuid(), Name = "Doc1" },
                new() { Id = Guid.NewGuid(), Name = "Doc2" }
            };
            _repository.GetAllAsync().Returns(entities);
            _mapper.Map<IEnumerable<DocumentDomain>>(entities).Returns(new List<DocumentDomain>
            {
                new() { Name = "Doc1" },
                new() { Name = "Doc2" }
            });

            // Act
            var result = await _sut.GetAllDocumentsAsync();

            // Assert
            Assert.Equal(2, result.Count());
            _logger.Received().LogInformation("Retrieved {DocumentCount} documents", 2);
        }

        [Fact]
        public async Task GetAllDocuments_RepositoryThrowsException_WrapsInDocumentProcessingException()
        {
            // Arrange
            _repository.GetAllAsync().Throws(new DocumentRepositoryException("DB error"));

            // Act & Assert
            var ex = await Assert.ThrowsAsync<DocumentProcessingException>(() => _sut.GetAllDocumentsAsync());
            Assert.Contains("Failed to retrieve documents from database", ex.Message);
        }

        [Fact]
        public async Task GetAllDocuments_UnexpectedException_WrapsInDocumentProcessingException()
        {
            // Arrange
            _repository.GetAllAsync().Throws(new Exception("Unexpected"));

            // Act & Assert
            var ex = await Assert.ThrowsAsync<DocumentProcessingException>(() => _sut.GetAllDocumentsAsync());
            Assert.Contains("An unexpected error occurred while retrieving documents", ex.Message);
        }

        #endregion

        #region GetDocumentByIdAsync Tests

        [Fact]
        public async Task GetDocumentById_Exists_ReturnsMappedDocument()
        {
            // Arrange
            var id = Guid.NewGuid();
            var entity = new DocumentEntity { Id = id, Name = "Test" };
            _repository.GetByIdAsync(id).Returns(entity);
            _mapper.Map<DocumentDomain>(entity).Returns(new DocumentDomain { Id = id, Name = "Test" });

            // Act
            var result = await _sut.GetDocumentByIdAsync(id);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(id, result.Id);
        }

        [Fact]
        public async Task GetDocumentById_NotFound_ReturnsNull()
        {
            // Arrange
            var id = Guid.NewGuid();
            _repository.GetByIdAsync(id).Returns((DocumentEntity?)null);

            // Act
            var result = await _sut.GetDocumentByIdAsync(id);

            // Assert
            Assert.Null(result);
            _logger.Received().LogError("Document with ID {DocumentId} not found", id);
        }

        [Fact]
        public async Task GetDocumentById_RepositoryThrowsException_WrapsInDocumentRetrievalException()
        {
            // Arrange
            var id = Guid.NewGuid();
            _repository.GetByIdAsync(id).Throws(new DocumentRepositoryException("DB error"));

            // Act & Assert
            var ex = await Assert.ThrowsAsync<DocumentRetrievalException>(() => _sut.GetDocumentByIdAsync(id));
            Assert.Contains($"Failed to retrieve document with document ID {id} from database", ex.Message);
        }

        [Fact]
        public async Task GetDocumentById_UnexpectedException_WrapsInDocumentRetrievalException()
        {
            // Arrange
            var id = Guid.NewGuid();
            _repository.GetByIdAsync(id).Throws(new Exception("Unexpected"));

            // Act & Assert
            var ex = await Assert.ThrowsAsync<DocumentRetrievalException>(() => _sut.GetDocumentByIdAsync(id));
            Assert.Contains($"Unexpected error while retrieving the document with document ID: {id}", ex.Message);
        }

        #endregion

        #region GetDocumentFileAsync Tests

        [Fact]
        public async Task GetDocumentFile_Success_ReturnsStream()
        {
            // Arrange
            var id = Guid.NewGuid();
            var entity = new DocumentEntity { Id = id, FilePath = "path/to/file.pdf" };
            _repository.GetByIdAsync(id).Returns(entity);
            var stream = new MemoryStream(new byte[] { 1, 2, 3 });
            _fileStorage.DownloadFileAsync("path/to/file.pdf").Returns(stream);

            // Act
            var result = await _sut.GetDocumentFileAsync(id);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(stream, result);
        }

        [Fact]
        public async Task GetDocumentFile_DocumentNotFound_ThrowsFileOperationException()
        {
            // Arrange
            var id = Guid.NewGuid();
            _repository.GetByIdAsync(id).Returns((DocumentEntity?)null);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<FileOperationException>(() => _sut.GetDocumentFileAsync(id));
            Assert.Contains($"Error accessing file for document {id}", ex.Message);
        }

        [Fact]
        public async Task GetDocumentFile_FilePathEmpty_ThrowsFileOperationException()
        {
            // Arrange
            var id = Guid.NewGuid();
            var entity = new DocumentEntity { Id = id, FilePath = "" };
            _repository.GetByIdAsync(id).Returns(entity);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<FileOperationException>(() => _sut.GetDocumentFileAsync(id));
            Assert.Contains($"Error accessing file for document {id}", ex.Message);
        }

        [Fact]
        public async Task GetDocumentFile_FileStorageFails_ThrowsFileOperationException()
        {
            // Arrange
            var id = Guid.NewGuid();
            var entity = new DocumentEntity { Id = id, FilePath = "path/to/file.pdf" };
            _repository.GetByIdAsync(id).Returns(entity);
            _fileStorage.DownloadFileAsync("path/to/file.pdf").Throws(new Exception("MinIO error"));

            // Act & Assert
            await Assert.ThrowsAsync<FileOperationException>(() => _sut.GetDocumentFileAsync(id));
        }

        [Fact]
        public async Task GetDocumentFile_RepositoryFails_ThrowsDocumentRetrievalException()
        {
            // Arrange
            var id = Guid.NewGuid();
            _repository.GetByIdAsync(id).Throws(new DocumentRepositoryException("DB error"));

            // Act & Assert
            await Assert.ThrowsAsync<DocumentRetrievalException>(() => _sut.GetDocumentFileAsync(id));
        }

        #endregion

        #region DeleteDocumentAsync Tests

        [Fact]
        public async Task DeleteDocument_Success_DeletesFromStorageAndDatabase()
        {
            // Arrange
            var id = Guid.NewGuid();
            var entity = new DocumentEntity { Id = id, FilePath = "path/to/file.pdf" };
            _repository.GetByIdAsync(id).Returns(entity);
            _repository.DeleteAsync(id).Returns(true);

            // Act
            var result = await _sut.DeleteDocumentAsync(id);

            // Assert
            Assert.True(result);
            await _fileStorage.Received(1).DeleteFileAsync("path/to/file.pdf");
            await _repository.Received(1).DeleteAsync(id);
            await _indexingService.Received(1).DeleteDocumentIndexAsync(id);
        }

        [Fact]
        public async Task DeleteDocument_NotFound_ReturnsFalse()
        {
            // Arrange
            var id = Guid.NewGuid();
            _repository.GetByIdAsync(id).Returns((DocumentEntity?)null);
            _repository.DeleteAsync(id).Returns(false);

            // Act
            var result = await _sut.DeleteDocumentAsync(id);

            // Assert
            Assert.False(result);
            await _fileStorage.DidNotReceive().DeleteFileAsync(Arg.Any<string>());
            await _indexingService.DidNotReceive().DeleteDocumentIndexAsync(Arg.Any<Guid>());
        }

        [Fact]
        public async Task DeleteDocument_EmptyFilePath_OnlyDeletesFromDatabase()
        {
            // Arrange
            var id = Guid.NewGuid();
            var entity = new DocumentEntity { Id = id, FilePath = "" };
            _repository.GetByIdAsync(id).Returns(entity);
            _repository.DeleteAsync(id).Returns(true);

            // Act
            var result = await _sut.DeleteDocumentAsync(id);

            // Assert
            Assert.True(result);
            await _fileStorage.DidNotReceive().DeleteFileAsync(Arg.Any<string>());
            await _repository.Received(1).DeleteAsync(id);
            await _indexingService.Received(1).DeleteDocumentIndexAsync(id);
        }

        [Fact]
        public async Task DeleteDocument_FileStorageFails_ContinuesWithDatabaseDeletion()
        {
            // Arrange
            var id = Guid.NewGuid();
            var entity = new DocumentEntity { Id = id, FilePath = "path/to/file.pdf" };
            _repository.GetByIdAsync(id).Returns(entity);
            _fileStorage.DeleteFileAsync("path/to/file.pdf").Throws(new Exception("MinIO error"));
            _repository.DeleteAsync(id).Returns(true);

            // Act
            var result = await _sut.DeleteDocumentAsync(id);

            // Assert
            Assert.True(result);
            _logger.Received().LogWarning(Arg.Any<Exception>(),
                "Failed to delete file from MinIO, continuing with DB deletion");
            await _repository.Received(1).DeleteAsync(id);
            await _indexingService.Received(1).DeleteDocumentIndexAsync(id);
        }

        [Fact]
        public async Task DeleteDocument_DatabaseFails_ThrowsDocumentProcessingException()
        {
            // Arrange
            var id = Guid.NewGuid();
            var entity = new DocumentEntity { Id = id, FilePath = "path/to/file.pdf" };
            _repository.GetByIdAsync(id).Returns(entity);
            _repository.DeleteAsync(id).Throws(new DocumentRepositoryException("DB error"));

            // Act & Assert
            await Assert.ThrowsAsync<DocumentProcessingException>(() => _sut.DeleteDocumentAsync(id));
        }

        [Fact]
        public async Task DeleteDocument_LogsWarningWhenStorageFails()
        {
            // Arrange
            var id = Guid.NewGuid();
            var entity = new DocumentEntity { Id = id, FilePath = "path/to/file.pdf" };
            _repository.GetByIdAsync(id).Returns(entity);
            var storageException = new Exception("Storage error");
            _fileStorage.DeleteFileAsync("path/to/file.pdf").Throws(storageException);
            _repository.DeleteAsync(id).Returns(true);

            // Act
            await _sut.DeleteDocumentAsync(id);

            // Assert
            _logger.Received(1).LogWarning(storageException,
                "Failed to delete file from MinIO, continuing with DB deletion");
            await _indexingService.Received(1).DeleteDocumentIndexAsync(id);
        }

        #endregion

        #region UpdateDocumentMetadataAsync Tests

        [Fact]
        public async Task UpdateMetadata_Success_UpdatesBothFields()
        {
            // Arrange
            var id = Guid.NewGuid();
            var name = "Updated Name";
            var summary = "Updated Summary";
            var entity = new DocumentEntity
            {
                Id = id,
                Name = name,
                GenAiSummary = summary,
                OcrText = "Some OCR text",
                Tags = ["tag1,tag2"]
            };
            var domain = new DocumentDomain
            {
                Id = id,
                Name = name,
                GenAiSummary = summary
            };

            _repository.UpdateMetadataAsync(id, name, summary).Returns(true);
            _repository.GetByIdAsync(id).Returns(entity);
            _mapper.Map<DocumentDomain>(entity).Returns(domain);

            // Act
            var result = await _sut.UpdateDocumentMetadataAsync(id, name, summary);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(id, result.Id);
            Assert.Equal(name, result.Name);
            Assert.Equal(summary, result.GenAiSummary);
            await _repository.Received(1).UpdateMetadataAsync(id, name, summary);
            await _indexingService.Received(1).IndexDocumentAsync(
                id,
                name,
                "Some OCR text",
                summary,
                Arg.Any<string[]>()
            );
        }

        [Fact]
        public async Task UpdateMetadata_Success_UpdatesOnlyName()
        {
            // Arrange
            var id = Guid.NewGuid();
            var name = "New Name";
            var entity = new DocumentEntity
            {
                Id = id,
                Name = name,
                OcrText = "",
                GenAiSummary = ""
            };
            var domain = new DocumentDomain { Id = id, Name = name };

            _repository.UpdateMetadataAsync(id, name, null).Returns(true);
            _repository.GetByIdAsync(id).Returns(entity);
            _mapper.Map<DocumentDomain>(entity).Returns(domain);

            // Act
            var result = await _sut.UpdateDocumentMetadataAsync(id, name, null);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(name, result.Name);
            await _repository.Received(1).UpdateMetadataAsync(id, name, null);
            await _indexingService.Received(1).IndexDocumentAsync(id, name, "", "", Arg.Any<string[]>());
        }

        [Fact]
        public async Task UpdateMetadata_Success_UpdatesOnlySummary()
        {
            // Arrange
            var id = Guid.NewGuid();
            var summary = "New Summary";
            var entity = new DocumentEntity
            {
                Id = id,
                Name = "Original Name",
                GenAiSummary = summary,
                OcrText = ""
            };
            var domain = new DocumentDomain { Id = id, GenAiSummary = summary };

            _repository.UpdateMetadataAsync(id, null, summary).Returns(true);
            _repository.GetByIdAsync(id).Returns(entity);
            _mapper.Map<DocumentDomain>(entity).Returns(domain);

            // Act
            var result = await _sut.UpdateDocumentMetadataAsync(id, null, summary);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(summary, result.GenAiSummary);
            await _repository.Received(1).UpdateMetadataAsync(id, null, summary);
            await _indexingService.Received(1).IndexDocumentAsync(
                id,
                "Original Name",
                "",
                summary,
                Arg.Any<string[]>()
            );
        }

        [Fact]
        public async Task UpdateMetadata_BothFieldsNull_ThrowsArgumentException()
        {
            // Arrange
            var id = Guid.NewGuid();

            // Act & Assert
            await Assert.ThrowsAsync<DocumentUpdateException>(() =>
                _sut.UpdateDocumentMetadataAsync(id, null, null));
            await _repository.DidNotReceive().UpdateMetadataAsync(Arg.Any<Guid>(), Arg.Any<string?>(), Arg.Any<string?>());
        }

        [Fact]
        public async Task UpdateMetadata_DocumentNotFound_ReturnsNull()
        {
            // Arrange
            var id = Guid.NewGuid();
            _repository.UpdateMetadataAsync(id, "name", null).Returns(false);

            // Act
            var result = await _sut.UpdateDocumentMetadataAsync(id, "name", null);

            // Assert
            Assert.Null(result);
            await _repository.DidNotReceive().GetByIdAsync(Arg.Any<Guid>());
            await _indexingService.DidNotReceive().IndexDocumentAsync(
                Arg.Any<Guid>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string[]>()
            );
        }

        [Fact]
        public async Task UpdateMetadata_DocumentNotFoundAfterUpdate_ReturnsNull()
        {
            // Arrange
            var id = Guid.NewGuid();
            _repository.UpdateMetadataAsync(id, "name", null).Returns(true);
            _repository.GetByIdAsync(id).Returns((DocumentEntity?)null);

            // Act
            var result = await _sut.UpdateDocumentMetadataAsync(id, "name", null);

            // Assert
            Assert.Null(result);
            await _indexingService.DidNotReceive().IndexDocumentAsync(
                Arg.Any<Guid>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string[]>()
            );
        }

        [Fact]
        public async Task UpdateMetadata_RepositoryFails_ThrowsDocumentUpdateException()
        {
            // Arrange
            var id = Guid.NewGuid();
            _repository.UpdateMetadataAsync(id, "name", null)
                .Throws(new DocumentRepositoryException("DB error"));

            // Act & Assert
            var ex = await Assert.ThrowsAsync<DocumentUpdateException>(() =>
                _sut.UpdateDocumentMetadataAsync(id, "name", null));
            Assert.Contains($"Failed to update document {id} metadata", ex.Message);
        }

        [Fact]
        public async Task UpdateMetadata_UnexpectedException_ThrowsDocumentUpdateException()
        {
            // Arrange
            var id = Guid.NewGuid();
            _repository.UpdateMetadataAsync(id, "name", null)
                .Throws(new Exception("Unexpected error"));

            // Act & Assert
            var ex = await Assert.ThrowsAsync<DocumentUpdateException>(() =>
                _sut.UpdateDocumentMetadataAsync(id, "name", null));
            Assert.Contains($"Unexpected error updating document {id} metadata", ex.Message);
        }

        #endregion

        #region SearchDocumentsAsync Tests

        [Fact]
        public async Task SearchDocuments_Success_ReturnsMappedResults()
        {
            // Arrange
            var query = "test query";
            var id1 = Guid.NewGuid();
            var id2 = Guid.NewGuid();
            var documentIds = new List<Guid> { id1, id2 };
            
            _searchService.SearchDocumentsAsync(query).Returns(documentIds);
            
            var entities = new List<DocumentEntity>
            {
                new() { Id = id1, Name = "Result1" },
                new() { Id = id2, Name = "Result2" }
            };
            _repository.GetByIdsAsync(documentIds).Returns(entities);
            
            _mapper.Map<IEnumerable<DocumentDomain>>(entities).Returns(new List<DocumentDomain>
            {
                new() { Id = id1, Name = "Result1" },
                new() { Id = id2, Name = "Result2" }
            });

            // Act
            var result = await _sut.SearchDocumentsAsync(query);

            // Assert
            Assert.Equal(2, result.Count());
            await _searchService.Received(1).SearchDocumentsAsync(query);
            await _repository.Received(1).GetByIdsAsync(documentIds);
            _logger.Received().LogInformation("Search completed. Found {ResultCount} documents", 2);
        }

        [Fact]
        public async Task SearchDocuments_NoResults_ReturnsEmptyList()
        {
            // Arrange
            var query = "no matches";
            _searchService.SearchDocumentsAsync(query).Returns(new List<Guid>());

            // Act
            var result = await _sut.SearchDocumentsAsync(query);

            // Assert
            Assert.Empty(result);
            await _searchService.Received(1).SearchDocumentsAsync(query);
            await _repository.DidNotReceive().GetByIdsAsync(Arg.Any<IEnumerable<Guid>>());
            _logger.Received().LogInformation("Search completed. No documents found for query: {SearchQuery}", query);
        }

        [Fact]
        public async Task SearchDocuments_SearchServiceFails_ThrowsDocumentProcessingException()
        {
            // Arrange
            var query = "test";
            _searchService.SearchDocumentsAsync(query).Throws(new Exception("ElasticSearch error"));

            // Act & Assert
            var ex = await Assert.ThrowsAsync<DocumentProcessingException>(() => _sut.SearchDocumentsAsync(query));
            Assert.Contains($"Failed to search documents with query: {query}", ex.Message);
        }

        [Fact]
        public async Task SearchDocuments_RepositoryFails_ThrowsDocumentProcessingException()
        {
            // Arrange
            var query = "test";
            var documentIds = new List<Guid> { Guid.NewGuid() };
            _searchService.SearchDocumentsAsync(query).Returns(documentIds);
            _repository.GetByIdsAsync(documentIds).Throws(new DocumentRepositoryException("DB error"));

            // Act & Assert
            var ex = await Assert.ThrowsAsync<DocumentProcessingException>(() => _sut.SearchDocumentsAsync(query));
            Assert.Contains($"Failed to retrieve documents for search query: {query}", ex.Message);
        }

        #endregion
    }
}