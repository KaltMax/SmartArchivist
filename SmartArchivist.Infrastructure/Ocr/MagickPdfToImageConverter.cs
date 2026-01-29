using ImageMagick;
using SmartArchivist.Contract.Abstractions.Ocr;

namespace SmartArchivist.Infrastructure.Ocr
{
    public class MagickPdfToImageConverter : IPdfToImageConverter
    {
        private readonly OcrConfig _config;

        public MagickPdfToImageConverter(OcrConfig config)
        {
            _config = config;
        }

        public async Task<IEnumerable<byte[]>> ConvertToImagesAsync(Stream pdfStream)
        {
            return await Task.Run(() =>
            {
                var images = new List<byte[]>();

                // Configure settings for high-quality image conversion
                var settings = new MagickReadSettings
                {
                    Density = new Density(_config.ConversionDpi)
                };

                // Load all pages from the PDF
                using var collection = new MagickImageCollection(pdfStream, settings);

                // Convert each page to PNG byte array
                foreach (var image in collection)
                {
                    image.Format = MagickFormat.Png;
                    images.Add(image.ToByteArray());
                }

                return images;
            });
        }
    }
}
