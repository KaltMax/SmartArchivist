namespace SmartArchivist.Contract.Abstractions.Ocr
{
    /// <summary>
    /// Defines a contract for converting PDF documents to image representations asynchronously.
    /// </summary>
    public interface IPdfToImageConverter
    {
        Task<IEnumerable<byte[]>> ConvertToImagesAsync(Stream pdfStream);
    }
}