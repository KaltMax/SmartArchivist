using SmartArchivist.Contract.Abstractions.Ocr;
using Tesseract;

namespace SmartArchivist.Infrastructure.Ocr
{
    /// <summary>
    /// Provides OCR services using the Tesseract engine for extracting text from image data.
    /// </summary>
    public class TesseractOcrService : IOcrService, IDisposable
    {
        private readonly TesseractEngine _engine;

        public TesseractOcrService(OcrConfig config)
        {
            var tessDataPath = Environment.GetEnvironmentVariable("TESSDATA_PREFIX") ?? config.TessDataPath;
            _engine = new TesseractEngine(tessDataPath, config.Languages, (EngineMode)config.EngineMode);
        }

        public Task<string> ExtractTextFromImageAsync(byte[] imageData)
        {
            using var img = Pix.LoadFromMemory(imageData);
            using var page = _engine.Process(img);
            return Task.FromResult(page.GetText());
        }

        public Task<string> ExtractTextFromImagesAsync(IEnumerable<byte[]> imagesData)
        {
            var textParts = new List<string>();

            foreach (var imageData in imagesData)
            {
                using var img = Pix.LoadFromMemory(imageData);
                using var page = _engine.Process(img);
                textParts.Add(page.GetText());
            }

            return Task.FromResult(string.Join("\n\n", textParts));
        }

        public void Dispose()
        {
            _engine.Dispose();
        }
    }
}
