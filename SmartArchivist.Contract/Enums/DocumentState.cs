namespace SmartArchivist.Contract.Enums
{
    /// <summary>
    /// Represents the current processing state of a document in the pipeline
    /// </summary>
    public enum DocumentState
    {
        Uploaded = 0,
        OcrCompleted = 1,
        GenAiCompleted = 2,
        Indexed = 3,
        Completed = 4,
        Failed = 99
    }
}