using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SmartArchivist.Application.DomainModels;
using SmartArchivist.Application.Exceptions;
using SmartArchivist.Contract;
using SmartArchivist.Contract.DTOs;
using SmartArchivist.Contract.Logger;
using SmartArchivist.Application.Services;

namespace SmartArchivist.Api.Controllers
{
    /// <summary>
    /// Provides API endpoints for managing documents, including uploading, retrieving, downloading, deleting, and
    /// searching documents. Requires authentication for all operations.
    /// </summary>
    [ApiController]
    [Route("api/documents")]
    [Authorize]
    public class DocumentController : ControllerBase
    {
        private readonly ILoggerWrapper<DocumentController> _logger;
        private readonly IDocumentService _documentService;
        private readonly IMapper _mapper;

        public DocumentController(ILoggerWrapper<DocumentController> logger, IDocumentService documentService, IMapper mapper)
        {
            _logger = logger;
            _documentService = documentService;
            _mapper = mapper;
        }

        [HttpPost("upload")]
        [ProducesResponseType(typeof(DocumentDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<DocumentDto>> UploadDocument([FromForm] DocumentUploadDto uploadDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var allowedExtensions = DocumentConstants.FileExtensions.AllowedExtensions;
                var fileExtension = Path.GetExtension(uploadDto.File.FileName).ToLowerInvariant();
                var contentType = uploadDto.File.ContentType;

                if (!allowedExtensions.Contains(fileExtension))
                {
                    return BadRequest("Unsupported file extension");
                }

                if (fileExtension == ".pdf" && contentType != "application/pdf") {
                    return BadRequest("File content type doesn't match extension");
                }

                if (uploadDto.File.Length > DocumentConstants.Limits.MaxFileSize)
                {
                    return BadRequest(
                        $"File size exceeds maximum limit of {DocumentConstants.Limits.MaxFileSize / (1024 * 1024)}MB");
                }

                if (uploadDto.Name.Length > DocumentConstants.Limits.MaxTitleLength)
                {
                    return BadRequest(
                        $"Document name exceeds maximum length of {DocumentConstants.Limits.MaxTitleLength} characters");
                }

                // Map to Domain
                var documentDomain = _mapper.Map<DocumentDomain>(uploadDto);

                // Extract file content
                using var memoryStream = new MemoryStream();
                await uploadDto.File.CopyToAsync(memoryStream);
                var fileContent = memoryStream.ToArray();

                // Call service
                var createdDocument = await _documentService.CreateDocumentAsync(documentDomain, fileContent);

                // Map back to Dto
                var result = _mapper.Map<DocumentDto>(createdDocument);

                _logger.LogInformation("Document uploaded successfully: {DocumentId}", createdDocument.Id);

                return CreatedAtAction(nameof(GetDocumentById), new { id = result.Id }, result);
            }
            catch (DuplicateDocumentException ex)
            {
                _logger.LogError(ex, "Duplicate document upload attempt: {DocumentName}", uploadDto.Name);
                return Conflict("A document with the same name already exists.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading document");
                return StatusCode(500, "Internal server error occurred while uploading document");
            }
        }

        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<DocumentDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<DocumentDto>>> GetAllDocuments()
        {
            try
            {
                var documentsDomain = await _documentService.GetAllDocumentsAsync();
                var documents = _mapper.Map<IEnumerable<DocumentDto>>(documentsDomain);

                _logger.LogInformation("Retrieved {Count} documents", documents.Count());

                return Ok(documents);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving documents");
                return StatusCode(500, "Internal server error occurred while retrieving documents");
            }
        }

        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<DocumentDto>> GetDocumentById(Guid id)
        {
            try
            {
                _logger.LogInformation("Retrieving document: {DocumentId}", id);

                var documentDomain = await _documentService.GetDocumentByIdAsync(id);

                if (documentDomain == null)
                {
                    return NotFound($"Document with ID {id} not found");
                }

                var document = _mapper.Map<DocumentDto>(documentDomain);
                return Ok(document);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving document {DocumentId}", id);
                return StatusCode(500, "Internal server error occurred while retrieving document");
            }
        }

        [HttpGet("{id}/download")]
        [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DownloadDocument(Guid id)
        {
            try
            {
                _logger.LogInformation("Downloading document: {DocumentId}", id);
            
                var document = await _documentService.GetDocumentByIdAsync(id);
                if (document == null)
                {
                    return NotFound();
                }

                var fileContent = await _documentService.GetDocumentFileAsync(id);
                return File(fileContent, document.ContentType, document.Name + document.FileExtension);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading document {DocumentId}", id);
                return StatusCode(500, "Internal server error occurred while downloading document");
            }
        }

        [HttpPatch("{id}")]
        [ProducesResponseType(typeof(DocumentDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<DocumentDto>> UpdateDocument(Guid id, [FromBody] DocumentUpdateDto updateDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Validate that at least one field is provided
                if (updateDto.Name == null && updateDto.Summary == null)
                {
                    return BadRequest("At least one field (name or summary) must be provided for update.");
                }

                if (updateDto.Name is { Length: > DocumentConstants.Limits.MaxTitleLength })
                {
                    return BadRequest(
                        $"Document name exceeds maximum length of {DocumentConstants.Limits.MaxTitleLength} characters");
                }

                _logger.LogInformation("Updating document {DocumentId} metadata", id);

                var updatedDocument = await _documentService.UpdateDocumentMetadataAsync(id, updateDto.Name, updateDto.Summary);

                if (updatedDocument == null)
                {
                    return NotFound($"Document with ID {id} not found");
                }

                var result = _mapper.Map<DocumentDto>(updatedDocument);
                _logger.LogInformation("Document {DocumentId} updated successfully", id);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating document {DocumentId} metadata", id);
                return StatusCode(500, "Internal server error occurred while updating document");
            }
        }

        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteDocument(Guid id)
        {
            try
            {
                _logger.LogInformation("Deleting document: {DocumentId}", id);

                var success = await _documentService.DeleteDocumentAsync(id);

                if (!success)
                {
                    return NotFound($"Document with ID {id} not found");
                }

                _logger.LogInformation("Document deleted successfully: {DocumentId}", id);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting document {DocumentId}", id);
                return StatusCode(500, "Internal server error occurred while deleting document");
            }
        }

        [HttpGet("search")]
        [ProducesResponseType(typeof(IEnumerable<DocumentDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<DocumentDto>>> SearchDocuments([FromQuery] string query)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query))
                {
                    return BadRequest("Search query cannot be empty");
                }

                _logger.LogInformation("Searching documents with query: {Query}", query);

                var documentsDomain = await _documentService.SearchDocumentsAsync(query);
                var documents = _mapper.Map<IEnumerable<DocumentDto>>(documentsDomain);

                _logger.LogInformation("Found {Count} documents matching query: {Query}", documents.Count(), query);

                return Ok(documents);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching documents with query: {Query}", query);
                return StatusCode(500, "Internal server error occurred while searching documents");
            }
        }
    }
}