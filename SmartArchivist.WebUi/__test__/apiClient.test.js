import { describe, it, expect } from 'vitest';
import { parseApiError } from '../src/api/ApiClient';

describe('parseApiError', () => {
  it('uses response.data when present', () => {
    const error = { response: { data: 'Resource not found' }, message: 'Request failed' };
    expect(parseApiError(error).message).toBe('Resource not found');
  });

  it('falls back to error.message when response is missing (network error)', () => {
    const error = { message: 'Network Error' };
    expect(parseApiError(error).message).toBe('Network Error');
  });

  it('falls back to error.message when response.data is empty', () => {
    const error = { response: { data: '' }, message: 'Bad Request' };
    expect(parseApiError(error).message).toBe('Bad Request');
  });

  it('returns an instance of Error', () => {
    const result = parseApiError({ message: 'oops' });
    expect(result).toBeInstanceOf(Error);
  });
});
