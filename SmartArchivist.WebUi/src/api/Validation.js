// Constants matching backend
export const VALIDATION_RULES = {
  MAX_FILE_SIZE: 10 * 1024 * 1024, // 10MB
  MAX_NAME_LENGTH: 255,
  MAX_DOCUMENTS: 10,
  ALLOWED_EXTENSIONS: ['.pdf'],
  MAX_SUMMARY_LENGTH: 5000
};

export const validateFile = (file) => {
  if (!file) {
    throw new Error('File is required');
  }
  if (file.size > VALIDATION_RULES.MAX_FILE_SIZE) {
    throw new Error(`File size exceeds ${VALIDATION_RULES.MAX_FILE_SIZE / (1024 * 1024)}MB`);
  }

  const ext = file.name.substring(file.name.lastIndexOf('.')).toLowerCase();
  if (!VALIDATION_RULES.ALLOWED_EXTENSIONS.includes(ext)) {
    throw new Error(`Only ${VALIDATION_RULES.ALLOWED_EXTENSIONS.join(', ')} files allowed`);
  }
};

export const validateName = (name) => {
  if (!name?.trim()) {
    throw new Error('Name is required');
  } 
  if (name.length > VALIDATION_RULES.MAX_NAME_LENGTH) {
    throw new Error(`Name too long (max ${VALIDATION_RULES.MAX_NAME_LENGTH} characters)`);
  }
};

export const validateSummary = (summary) => {
  if (summary && summary.length > VALIDATION_RULES.MAX_SUMMARY_LENGTH) {
    throw new Error(`Summary too long (max ${VALIDATION_RULES.MAX_SUMMARY_LENGTH} characters)`);
  }
};

export const validateId = (id) => {
  if (!id) {
    throw new Error('ID is required');
  }
}

export const validateDocumentDto = (doc) => {
  if (!doc || typeof doc !== 'object') {
    throw new Error('Invalid document data received');
  }

  const requiredFields = ['id', 'name', 'filePath', 'fileExtension', 'contentType', 'uploadDate', 'fileSize'];
  const missingFields = requiredFields.filter(field => !doc[field]);

  if (missingFields.length > 0) {
    throw new Error(`Document missing required fields: ${missingFields.join(', ')}`);
  }

  return doc;
};

export const validateDocumentArray = (docs) => {
  if (!Array.isArray(docs)) {
    throw new Error('Expected an array of documents');
  }
  return docs.map(validateDocumentDto);
}
