import { describe, it, expect } from 'vitest';
import { DocumentState, formatDocumentState } from '../src/utils/formatDocumentState';

describe('formatDocumentState', () => {
  it.each([
    [DocumentState.Uploaded, 'Uploaded'],
    [DocumentState.OcrCompleted, 'OCR Completed'],
    [DocumentState.GenAiCompleted, 'AI Processing Completed'],
    [DocumentState.Indexed, 'Indexed'],
    [DocumentState.Completed, 'Completed'],
    [DocumentState.Failed, 'Failed'],
  ])('maps state %i to %s', (state, expected) => {
    expect(formatDocumentState(state)).toBe(expected);
  });

  it('returns "Unknown" for unrecognized values', () => {
    expect(formatDocumentState(123)).toBe('Unknown');
  });

  it('coerces numeric strings before matching', () => {
    expect(formatDocumentState('4')).toBe('Completed');
  });

  it('returns "Unknown" for non-numeric strings', () => {
    expect(formatDocumentState('not a number')).toBe('Unknown');
  });
});
