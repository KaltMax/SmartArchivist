namespace SmartArchivist.Application.Exceptions
{
    /// <summary>
    /// Represents errors that occur during document update operations.
    /// </summary>
    public class DocumentUpdateException : Exception
    {
        public DocumentUpdateException() { }

        public DocumentUpdateException(string? message) : base(message) { }

        public DocumentUpdateException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}
