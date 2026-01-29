// Maps DocumentState enum values to user-friendly display strings Based on SmartArchivist.Contract.Enums.DocumentState
export const DocumentState = {
  Uploaded: 0,
  OcrCompleted: 1,
  GenAiCompleted: 2,
  Indexed: 3,
  Completed: 4,
  Failed: 99
};


// Converts a DocumentState value to a readable string
export function formatDocumentState(state) {
  const stateValue = typeof state === 'string' ? parseInt(state, 10) : state;

  switch (stateValue) {
    case DocumentState.Uploaded:
      return "Uploaded";
    case DocumentState.OcrCompleted:
      return "OCR Completed";
    case DocumentState.GenAiCompleted:
      return "AI Processing Completed";
    case DocumentState.Indexed:
      return "Indexed";
    case DocumentState.Completed:
      return "Completed";
    case DocumentState.Failed:
      return "Failed";
    default:
      return "Unknown";
  }
}