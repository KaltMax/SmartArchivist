namespace SmartArchivist.Application.Exceptions
{
    /// <summary>
    /// Represents errors that occur during document processing operations (e.g. ocr or gen-ai processing).
    /// </summary>
    public class DocumentProcessingException : Exception
    {
        public DocumentProcessingException() { }

        public DocumentProcessingException(string? message) : base(message) { }

        public DocumentProcessingException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}
