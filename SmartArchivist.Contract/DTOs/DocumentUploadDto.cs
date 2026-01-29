using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace SmartArchivist.Contract.DTOs
{
    /// <summary>
    /// Represents the data required to upload a document.
    /// </summary>
    public class DocumentUploadDto
    {
        [Required]
        public IFormFile File { get; set; } = null!;

        [Required]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;
    }
}