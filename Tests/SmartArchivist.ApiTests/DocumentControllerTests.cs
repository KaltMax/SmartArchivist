using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SmartArchivist.Application.DomainModels;
using SmartArchivist.Application.Exceptions;
using SmartArchivist.Application.Services;
using SmartArchivist.Contract.DTOs;
using SmartArchivist.Contract.Logger;
using SmartArchivist.Api.Controllers;

namespace Tests.SmartArchivist.ApiTests
{
    public class DocumentControllerTests
    {
        private readonly IDocumentService _service;
        private readonly IMapper _mapper;
        private readonly ILoggerWrapper<DocumentController> _logger;
        private readonly DocumentController _controller;

        public DocumentControllerTests()
        {
            _service = Substitute.For<IDocumentService>();
            _mapper = Substitute.For<IMapper>();
            _logger = Substitute.For<ILoggerWrapper<DocumentController>>();
            _controller = new DocumentController(_logger, _service, _mapper);
        }

        [Fact]
        public async Task UploadDocument_Should_Return_BadRequest_When_FileExtension_Invalid()
        {
            // Arrange
            var uploadDto = CreateUploadDto("test.txt"); // .txt not allowed

            // Act
            var result = await _controller.UploadDocument(uploadDto);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("Unsupported file extension", badRequestResult.Value);
        }

        [Fact]
        public async Task UploadDocument_Should_Return_BadRequest_When_FileSize_Exceeds_Limit()
        {
            // Arrange
            var largeFile = CreateMockFile("test.pdf", 100 * 1024 * 1024); // 100MB
            var uploadDto = new DocumentUploadDto { File = largeFile, Name = "Test" };

            // Act
            var result = await _controller.UploadDocument(uploadDto);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Contains("File size exceeds", badRequestResult.Value?.ToString());
        }

        [Fact]
        public async Task UploadDocument_Should_Return_BadRequest_When_ContentType_Mismatch()
        {
            // Arrange
            var file = CreateMockFile("test.pdf", 1024, "text/plain"); // Wrong content type
            var uploadDto = new DocumentUploadDto { File = file, Name = "Test" };

            // Act
            var result = await _controller.UploadDocument(uploadDto);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("File content type doesn't match extension", badRequestResult.Value);
        }

        [Fact]
        public async Task UploadDocument_Should_Return_201Created_When_Valid()
        {
            // Arrange
            var uploadDto = CreateUploadDto("test.pdf");
            var domain = new DocumentDomain { Id = Guid.NewGuid(), Name = "Test" };
            var dto = new DocumentDto { Id = domain.Id, Name = "Test" };

            _mapper.Map<DocumentDomain>(uploadDto).Returns(domain);
            _service.CreateDocumentAsync(Arg.Any<DocumentDomain>(), Arg.Any<byte[]>()).Returns(domain);
            _mapper.Map<DocumentDto>(domain).Returns(dto);

            // Act
            var result = await _controller.UploadDocument(uploadDto);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            Assert.Equal(nameof(DocumentController.GetDocumentById), createdResult.ActionName);
            var returnedDto = Assert.IsType<DocumentDto>(createdResult.Value);
            Assert.Equal(dto.Id, returnedDto.Id);
        }

        [Fact]
        public async Task UploadDocument_Should_Return_Conflict_When_Duplicate()
        {
            // Arrange
            var uploadDto = CreateUploadDto("test.pdf");
            _mapper.Map<DocumentDomain>(uploadDto).Returns(new DocumentDomain());
            _service.CreateDocumentAsync(Arg.Any<DocumentDomain>(), Arg.Any<byte[]>())
                .Throws(new DuplicateDocumentException("Duplicate"));

            // Act
            var result = await _controller.UploadDocument(uploadDto);

            // Assert
            var conflictResult = Assert.IsType<ConflictObjectResult>(result.Result);
            Assert.Equal("A document with the same name already exists.", conflictResult.Value);
        }

        [Fact]
        public async Task UploadDocument_Should_Return_500InternalServerError_On_Exception()
        {
            // Arrange
            var uploadDto = CreateUploadDto("test.pdf");
            _mapper.Map<DocumentDomain>(uploadDto).Returns(new DocumentDomain());
            _service.CreateDocumentAsync(Arg.Any<DocumentDomain>(), Arg.Any<byte[]>())
                .Throws(new Exception("Unexpected error"));

            // Act
            var result = await _controller.UploadDocument(uploadDto);

            // Assert
            var objectResult = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(500, objectResult.StatusCode);
            Assert.Equal("Internal server error occurred while uploading document", objectResult.Value);
        }

        [Fact]
        public async Task GetAllDocuments_Should_Return_200OK_With_Documents()
        {
            // Arrange
            var domains = new List<DocumentDomain>
              {
                  new() { Id = Guid.NewGuid(), Name = "Doc1" },
                  new() { Id = Guid.NewGuid(), Name = "Doc2" }
              };
            var dtos = new List<DocumentDto>
              {
                  new() { Id = domains[0].Id, Name = "Doc1" },
                  new() { Id = domains[1].Id, Name = "Doc2" }
              };

            _service.GetAllDocumentsAsync().Returns(domains);
            _mapper.Map<IEnumerable<DocumentDto>>(domains).Returns(dtos);

            // Act
            var result = await _controller.GetAllDocuments();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnedDtos = Assert.IsType<List<DocumentDto>>(okResult.Value);
            Assert.Equal(2, returnedDtos.Count);
        }

        [Fact]
        public async Task GetAllDocuments_Should_Return_500InternalServerError_On_Exception()
        {
            // Arrange
            _service.GetAllDocumentsAsync().Throws(new Exception("Unexpected error"));

            // Act
            var result = await _controller.GetAllDocuments();

            // Assert
            var objectResult = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(500, objectResult.StatusCode);
            Assert.Equal("Internal server error occurred while retrieving documents", objectResult.Value);
        }

        [Fact]
        public async Task GetDocumentById_Should_Return_200OK_When_Found()
        {
            // Arrange
            var id = Guid.NewGuid();
            var domain = new DocumentDomain { Id = id, Name = "Test" };
            var dto = new DocumentDto { Id = id, Name = "Test" };

            _service.GetDocumentByIdAsync(id).Returns(domain);
            _mapper.Map<DocumentDto>(domain).Returns(dto);

            // Act
            var result = await _controller.GetDocumentById(id);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnedDto = Assert.IsType<DocumentDto>(okResult.Value);
            Assert.Equal(id, returnedDto.Id);
        }

        [Fact]
        public async Task GetDocumentById_Should_Return_404NotFound_When_Not_Exists()
        {
            // Arrange
            var id = Guid.NewGuid();
            _service.GetDocumentByIdAsync(id).Returns((DocumentDomain?)null);

            // Act
            var result = await _controller.GetDocumentById(id);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
            Assert.Contains(id.ToString(), notFoundResult.Value?.ToString());
        }

        [Fact]
        public async Task GetDocumentById_Should_Return_500InternalServerError_On_Exception()
        {
            // Arrange
            var id = Guid.NewGuid();
            _service.GetDocumentByIdAsync(id).Throws(new Exception("Unexpected error"));

            // Act
            var result = await _controller.GetDocumentById(id);

            // Assert
            var objectResult = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(500, objectResult.StatusCode);
            Assert.Equal("Internal server error occurred while retrieving document", objectResult.Value);
        }

        [Fact]
        public async Task DeleteDocument_Should_Return_204NoContent_When_Deleted()
        {
            // Arrange
            var id = Guid.NewGuid();
            _service.DeleteDocumentAsync(id).Returns(true);

            // Act
            var result = await _controller.DeleteDocument(id);

            // Assert
            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task DeleteDocument_Should_Return_404NotFound_When_Not_Exists()
        {
            // Arrange
            var id = Guid.NewGuid();
            _service.DeleteDocumentAsync(id).Returns(false);

            // Act
            var result = await _controller.DeleteDocument(id);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Contains(id.ToString(), notFoundResult.Value?.ToString());
        }

        [Fact]
        public async Task DelteDocument_Should_Return_500InternalServerError_On_Exception()
        {
            // Arrange
            var id = Guid.NewGuid();
            _service.DeleteDocumentAsync(id).Throws(new Exception("Unexpected error"));

            // Act
            var result = await _controller.DeleteDocument(id);

            // Assert
            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, objectResult.StatusCode);
            Assert.Equal("Internal server error occurred while deleting document", objectResult.Value);
        }

        [Fact]
        public async Task DownloadDocument_Should_Return_File_With_Correct_ContentType_And_Name()
        {
            // Arrange
            var id = Guid.NewGuid();
            _service.GetDocumentByIdAsync(id).Returns(new DocumentDomain
            {
                Name = "Invoice2024",
                FileExtension = ".pdf",
                ContentType = "application/pdf"
            });
            _service.GetDocumentFileAsync(id).Returns(new MemoryStream(new byte[10]));

            // Act
            var result = await _controller.DownloadDocument(id);

            // Assert
            var fileResult = Assert.IsType<FileStreamResult>(result);
            Assert.Equal("application/pdf", fileResult.ContentType);
            Assert.Equal("Invoice2024.pdf", fileResult.FileDownloadName);
        }

        [Fact]
        public async Task DownloadDocument_Should_Return_NotFound_When_Document_Null()
        {
            // Arrange
            _service.GetDocumentByIdAsync(Arg.Any<Guid>()).Returns((DocumentDomain?)null);

            // Act
            var result = await _controller.DownloadDocument(Guid.NewGuid());

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task DownloadDocument_Should_Return_500InternalServerError_On_Exception()
        {
            // Arrange
            var id = Guid.NewGuid();
            _service.GetDocumentByIdAsync(id).Throws(new Exception("Unexpected error"));

            // Act
            var result = await _controller.DownloadDocument(id);

            // Assert
            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, objectResult.StatusCode);
            Assert.Equal("Internal server error occurred while downloading document", objectResult.Value);
        }

        [Fact]
        public async Task SearchDocuments_Should_Return_BadRequest_When_Query_Empty()
        {
            // Act
            var result = await _controller.SearchDocuments("");

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("Search query cannot be empty", badRequestResult.Value);
        }

        [Fact]
        public async Task SearchDocuments_Should_Return_200OK_With_Results()
        {
            // Arrange
            var query = "test";
            var domains = new List<DocumentDomain> { new() { Id = Guid.NewGuid(), Name = "TestDoc" } };
            var dtos = new List<DocumentDto> { new() { Id = domains[0].Id, Name = "TestDoc" } };

            _service.SearchDocumentsAsync(query).Returns(domains);
            _mapper.Map<IEnumerable<DocumentDto>>(domains).Returns(dtos);

            // Act
            var result = await _controller.SearchDocuments(query);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnedDtos = Assert.IsAssignableFrom<IEnumerable<DocumentDto>>(okResult.Value);
            Assert.Single(returnedDtos);
        }

        [Fact]
        public async Task SearchDocuments_Should_Return_500InternalServerError_On_Exception()
        {
            // Arrange
            var query = "test";
            _service.SearchDocumentsAsync(query).Throws(new Exception("Unexpected error"));

            // Act
            var result = await _controller.SearchDocuments(query);

            // Assert
            var objectResult = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(500, objectResult.StatusCode);
            Assert.Equal("Internal server error occurred while searching documents", objectResult.Value);
        }

        [Fact]
        public async Task UpdateDocument_Should_Return_200OK_When_Updated()
        {
            // Arrange
            var id = Guid.NewGuid();
            var updateDto = new DocumentUpdateDto { Name = "Updated Name", Summary = "Updated Summary" };
            var domain = new DocumentDomain { Id = id, Name = "Updated Name", GenAiSummary = "Updated Summary" };
            var dto = new DocumentDto { Id = id, Name = "Updated Name", GenAiSummary = "Updated Summary" };

            _service.UpdateDocumentMetadataAsync(id, updateDto.Name, updateDto.Summary).Returns(domain);
            _mapper.Map<DocumentDto>(domain).Returns(dto);

            // Act
            var result = await _controller.UpdateDocument(id, updateDto);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnedDto = Assert.IsType<DocumentDto>(okResult.Value);
            Assert.Equal(id, returnedDto.Id);
            Assert.Equal("Updated Name", returnedDto.Name);
        }

        [Fact]
        public async Task UpdateDocument_Should_Return_404NotFound_When_Not_Exists()
        {
            // Arrange
            var id = Guid.NewGuid();
            var updateDto = new DocumentUpdateDto { Name = "Updated Name" };
            _service.UpdateDocumentMetadataAsync(id, updateDto.Name, updateDto.Summary).Returns((DocumentDomain?)null);

            // Act
            var result = await _controller.UpdateDocument(id, updateDto);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
            Assert.Contains(id.ToString(), notFoundResult.Value?.ToString());
        }

        [Fact]
        public async Task UpdateDocument_Should_Return_BadRequest_When_Both_Fields_Null()
        {
            // Arrange
            var id = Guid.NewGuid();
            var updateDto = new DocumentUpdateDto { Name = null, Summary = null };

            // Act
            var result = await _controller.UpdateDocument(id, updateDto);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("At least one field (name or summary) must be provided for update.", badRequestResult.Value);
        }

        [Fact]
        public async Task UpdateDocument_Should_Return_BadRequest_When_ModelState_Invalid()
        {
            // Arrange
            var id = Guid.NewGuid();
            var updateDto = new DocumentUpdateDto { Name = "Valid Name" };
            _controller.ModelState.AddModelError("Summary", "Summary is too long");

            // Act
            var result = await _controller.UpdateDocument(id, updateDto);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task UpdateDocument_Should_Return_500InternalServerError_On_Exception()
        {
            // Arrange
            var id = Guid.NewGuid();
            var updateDto = new DocumentUpdateDto { Name = "Updated Name" };
            _service.UpdateDocumentMetadataAsync(id, updateDto.Name, updateDto.Summary)
                .Throws(new Exception("Unexpected error"));

            // Act
            var result = await _controller.UpdateDocument(id, updateDto);

            // Assert
            var objectResult = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(500, objectResult.StatusCode);
            Assert.Equal("Internal server error occurred while updating document", objectResult.Value);
        }

        [Fact]
        public async Task UpdateDocument_Should_Update_Only_Name_When_Summary_Null()
        {
            // Arrange
            var id = Guid.NewGuid();
            var updateDto = new DocumentUpdateDto { Name = "New Name", Summary = null };
            var domain = new DocumentDomain { Id = id, Name = "New Name" };
            var dto = new DocumentDto { Id = id, Name = "New Name" };

            _service.UpdateDocumentMetadataAsync(id, "New Name", null).Returns(domain);
            _mapper.Map<DocumentDto>(domain).Returns(dto);

            // Act
            var result = await _controller.UpdateDocument(id, updateDto);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnedDto = Assert.IsType<DocumentDto>(okResult.Value);
            Assert.Equal("New Name", returnedDto.Name);
        }

        [Fact]
        public async Task UpdateDocument_Should_Update_Only_Summary_When_Name_Null()
        {
            // Arrange
            var id = Guid.NewGuid();
            var updateDto = new DocumentUpdateDto { Name = null, Summary = "New Summary" };
            var domain = new DocumentDomain { Id = id, GenAiSummary = "New Summary" };
            var dto = new DocumentDto { Id = id, GenAiSummary = "New Summary" };

            _service.UpdateDocumentMetadataAsync(id, null, "New Summary").Returns(domain);
            _mapper.Map<DocumentDto>(domain).Returns(dto);

            // Act
            var result = await _controller.UpdateDocument(id, updateDto);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnedDto = Assert.IsType<DocumentDto>(okResult.Value);
            Assert.Equal("New Summary", returnedDto.GenAiSummary);
        }

        // Helper methods
        private DocumentUploadDto CreateUploadDto(string fileName)
        {
            var file = CreateMockFile(fileName, 1024);
            return new DocumentUploadDto { File = file, Name = "Test" };
        }

        private IFormFile CreateMockFile(string fileName, long size, string contentType = "application/pdf")
        {
            var file = Substitute.For<IFormFile>();
            file.FileName.Returns(fileName);
            file.Length.Returns(size);
            file.ContentType.Returns(contentType);
            file.CopyToAsync(Arg.Any<Stream>()).Returns(Task.CompletedTask);
            return file;
        }
    }
}