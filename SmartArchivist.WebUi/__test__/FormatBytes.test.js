import { describe, it, expect } from 'vitest'
import { formatBytes } from '../src/utils/formatBytes'

describe('Utils', () => {
  describe('formatBytes', () => {
    it('should return "0 B" for zero bytes', () => {
      expect(formatBytes(0)).toBe('0 B')
    })

    it('should format bytes correctly', () => {
      expect(formatBytes(500)).toBe('500.00 B')
    })

    it('should format kilobytes correctly', () => {
      expect(formatBytes(1024)).toBe('1.00 KB')
      expect(formatBytes(2048)).toBe('2.00 KB')
    })

    it('should format megabytes correctly', () => {
      expect(formatBytes(1048576)).toBe('1.00 MB')
      expect(formatBytes(5242880)).toBe('5.00 MB')
    })

    it('should format gigabytes correctly', () => {
      expect(formatBytes(1073741824)).toBe('1.00 GB')
    })

    it('should format decimal values correctly', () => {
      expect(formatBytes(1536)).toBe('1.50 KB')
    })
  })
})
