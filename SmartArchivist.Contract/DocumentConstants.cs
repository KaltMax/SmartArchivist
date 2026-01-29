namespace SmartArchivist.Contract
{
    /// <summary>
    /// Provides constant values related to document file extensions, and limits for use throughout the application.
    /// </summary>
    public static class DocumentConstants
    {
        public static class FileExtensions
        {
            public static readonly string[] AllowedExtensions = { ".pdf" };
        }

        public static class Limits
        {
            public const long MaxFileSize = 10 * 1024 * 1024; // 10MB
            public const int MaxTitleLength = 255;
        }
    }
}
