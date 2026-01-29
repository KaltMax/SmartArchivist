namespace SmartArchivist.Application.Exceptions
{
    /// <summary>
    /// Represents an exception that is thrown when an attempt is made to add a document that already exists in the
    /// collection.
    /// </summary>
    public class DuplicateDocumentException : Exception
    {
        public DuplicateDocumentException() { }

        public DuplicateDocumentException(string? message) : base(message) { }

        public DuplicateDocumentException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}
