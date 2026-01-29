import axios from 'axios';
import { validateFile, validateName, validateDocumentDto } from './Validation';

const API_BASE_URL = '/api';

export const uploadDocument = async (file, name) => {
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
    console.log(error);
    const errorMessage = error.response?.data || error.message;
    throw new Error(errorMessage);
  }
};
