namespace SmartArchivist.Application.Exceptions
{
    /// <summary>
    /// Represents errors that occur during file operations.
    /// </summary>
    public class FileOperationException : Exception
    {
        public FileOperationException() { }

        public FileOperationException(string? message) : base(message) { }

        public FileOperationException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}
