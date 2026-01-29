namespace SmartArchivist.Application.Exceptions
{
    /// <summary>
    /// Represents errors that occur during document retrieval operations.
    /// </summary>
    public class DocumentRetrievalException : Exception
    {
        public DocumentRetrievalException() { }

        public DocumentRetrievalException(string? message) : base(message) { }

        public DocumentRetrievalException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}
