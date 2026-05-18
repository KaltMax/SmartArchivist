export const API_BASE_URL = '/api';

// Converts an axios error to a plain Error with a usable message.
// The backend returns either a string body or, on network failure, axios fills in the message.
export function parseApiError(error) {
  return new Error(error.response?.data || error.message);
}
