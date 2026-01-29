namespace SmartArchivist.Contract
{
    /// <summary>
    /// Provides constant queue name values used for messaging within the SmartArchivist system.
    /// </summary>
    public static class QueueNames
    {
        public const string OcrQueue = "smartarchivist.ocr";
        public const string GenAiQueue = "smartarchivist.genai";
        public const string IndexingQueue = "smartarchivist.indexing";
        public const string DocumentResultQueue = "smartarchivist.document.result";
    }
}
