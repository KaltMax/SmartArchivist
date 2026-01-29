namespace SmartArchivist.Contract.Abstractions.Ocr
{
    /// <summary>
    /// Defines a contract for ocr services that extract text from image data.
    /// </summary>
    public interface IOcrService
    {
        Task<string> ExtractTextFromImageAsync(byte[] imageData);
        Task<string> ExtractTextFromImagesAsync(IEnumerable<byte[]> imagesData);
    }
}