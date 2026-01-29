using System.ComponentModel.DataAnnotations;

namespace SmartArchivist.Contract.DTOs
{
    /// <summary>
    /// DTO for updating document metadata (name and/or summary).
    /// </summary>
    public class DocumentUpdateDto
    {
        [StringLength(255, MinimumLength = 1)]
        public string? Name { get; set; }

        [StringLength(5000)]
        public string? Summary { get; set; }
    }
}
