using System.ComponentModel.DataAnnotations;

namespace SmartArchivist.Infrastructure.Ocr
{
    /// <summary>
    /// Configuration settings for OCR processing using Tesseract and ImageMagick.
    /// </summary>
    public class OcrConfig
    {
        [Required]
        public string TessDataPath { get; set; } = "/usr/share/tessdata";
        [Required]
        public string Languages { get; set; } = "eng+deu";
        [Range(0, 3)]
        public int EngineMode { get; set; } = 3;
        [Range(72, 600)]
        public int ConversionDpi { get; set; } = 300;
    }
}
