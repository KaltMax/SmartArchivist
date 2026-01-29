namespace SmartArchivist.Dal.Exceptions
{
    /// <summary>
    /// Represents errors that occur during operations in a document repository.
    /// </summary>
    public sealed class DocumentRepositoryException : Exception
    {
        public DocumentRepositoryException() { }

        public DocumentRepositoryException(string? message)
            : base(message) { }

        public DocumentRepositoryException(string? message, Exception? innerException)
            : base(message, innerException) { }
    }
}