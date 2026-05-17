import axios from 'axios';
import { validateFile, validateName, validateDocumentDto } from './Validation';
import { API_BASE_URL, parseApiError } from './apiClient';

export async function uploadDocument(file, name) {
  validateFile(file);
  validateName(name);

  const formData = new FormData();
  formData.append('File', file);
  formData.append('Name', name);

  try {
    const response = await axios.post(`${API_BASE_URL}/documents/upload`, formData, {
      headers: {
        'Content-Type': 'multipart/form-data',
      },
    });
    return validateDocumentDto(response.data);
  } catch (error) {
    throw parseApiError(error);
  }
}
