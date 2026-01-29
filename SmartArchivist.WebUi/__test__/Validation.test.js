import { describe, it, expect } from 'vitest'
import {
  VALIDATION_RULES,
  validateFile,
  validateName,
  validateSummary,
  validateId,
  validateDocumentDto,
  validateDocumentArray
} from '../src/api/Validation'

describe('Validation', () => {
  describe('validateFile', () => {
    it('should throw error when file is missing', () => {
      expect(() => validateFile(null)).toThrow('File is required')
    })

    it('should throw error when file size exceeds limit', () => {
      const largeFile = { size: VALIDATION_RULES.MAX_FILE_SIZE + 1, name: 'test.pdf' }
      expect(() => validateFile(largeFile)).toThrow('File size exceeds')
    })

    it('should throw error for non-PDF files', () => {
      expect(() => validateFile({ size: 1000, name: 'test.docx' })).toThrow('Only .pdf files allowed')
    })

    it('should accept valid PDF file', () => {
      expect(() => validateFile({ size: 1000, name: 'test.pdf' })).not.toThrow()
    })
  })

  describe('validateName', () => {
    it('should throw error when name is empty', () => {
      expect(() => validateName('')).toThrow('Name is required')
    })

    it('should throw error when name is only whitespace', () => {
      expect(() => validateName('   ')).toThrow('Name is required')
    })

    it('should throw error when name exceeds max length', () => {
      const longName = 'a'.repeat(VALIDATION_RULES.MAX_NAME_LENGTH + 1)
      expect(() => validateName(longName)).toThrow('Name too long')
    })

    it('should accept valid name', () => {
      expect(() => validateName('Valid Document Name')).not.toThrow()
    })
  })

  describe('validateSummary', () => {
    it('should throw error when summary exceeds max length', () => {
      const longSummary = 'a'.repeat(VALIDATION_RULES.MAX_SUMMARY_LENGTH + 1)
      expect(() => validateSummary(longSummary)).toThrow('Summary too long')
    })

    it('should accept valid summary', () => {
      expect(() => validateSummary('Valid summary text')).not.toThrow()
    })

    it('should accept null or undefined summary', () => {
      expect(() => validateSummary(null)).not.toThrow()
      expect(() => validateSummary(undefined)).not.toThrow()
    })
  })

  describe('validateId', () => {
    it('should throw error when id is missing', () => {
      expect(() => validateId(null)).toThrow('ID is required')
    })

    it('should throw error when id is undefined', () => {
      expect(() => validateId(undefined)).toThrow('ID is required')
    })

    it('should accept valid numeric id', () => {
      expect(() => validateId(123)).not.toThrow()
    })

    it('should accept valid string id', () => {
      expect(() => validateId('123')).not.toThrow()
    })
  })

  describe('validateDocumentDto', () => {
    const validDoc = {
      id: '123',
      name: 'Test Doc',
      filePath: '/path/to/doc.pdf',
      fileExtension: '.pdf',
      contentType: 'application/pdf',
      uploadDate: '2024-01-01',
      fileSize: 1000
    }

    it('should throw error when doc is invalid', () => {
      expect(() => validateDocumentDto(null)).toThrow('Invalid document data')
    })

    it('should throw error when required fields are missing', () => {
      const docWithoutId = { ...validDoc }
      delete docWithoutId.id
      expect(() => validateDocumentDto(docWithoutId)).toThrow('Document missing required fields')
    })

    it('should return valid document', () => {
      const result = validateDocumentDto(validDoc)
      expect(result).toEqual(validDoc)
    })
  })

  describe('validateDocumentArray', () => {
    it('should throw error when input is not an array', () => {
      expect(() => validateDocumentArray(null)).toThrow('Expected an array')
    })

    it('should accept empty array', () => {
      expect(() => validateDocumentArray([])).not.toThrow()
    })

    it('should validate array of documents', () => {
      const docs = [{
        id: '123',
        name: 'Test',
        filePath: '/test.pdf',
        fileExtension: '.pdf',
        contentType: 'application/pdf',
        uploadDate: '2024-01-01',
        fileSize: 1000
      }]
      expect(() => validateDocumentArray(docs)).not.toThrow()
    })

    it('should throw error when array contains invalid document', () => {
      const docs = [{
        id: '123',
        name: 'Test'
        // missing required fields
      }]
      expect(() => validateDocumentArray(docs)).toThrow('Document missing required fields')
    })
  })
})
