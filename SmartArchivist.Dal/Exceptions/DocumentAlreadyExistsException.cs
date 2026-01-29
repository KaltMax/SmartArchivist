namespace SmartArchivist.Dal.Exceptions
{
    /// <summary>
    /// Represents an exception that is thrown when an attempt is made to create a document that already exists.
    /// </summary>
    public sealed class DocumentAlreadyExistsException : Exception
    {
        public DocumentAlreadyExistsException() { }

        public DocumentAlreadyExistsException(string? message)
            : base(message) { }

        public DocumentAlreadyExistsException(string? message, Exception? innerException)
            : base(message, innerException) { }
    }
}
