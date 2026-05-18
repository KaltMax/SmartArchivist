import { describe, it, expect, vi, afterEach } from 'vitest';
import { triggerDownload } from '../src/utils/triggerDownload';

describe('triggerDownload', () => {
  afterEach(() => {
    document.body.innerHTML = '';
    vi.restoreAllMocks();
  });

  it('creates an anchor with the given url and filename, clicks it, and removes it', () => {
    const created = [];
    const realCreateElement = document.createElement.bind(document);
    vi.spyOn(document, 'createElement').mockImplementation((tag) => {
      const el = realCreateElement(tag);
      if (tag === 'a') {
        created.push(el);
        vi.spyOn(el, 'click');
      }
      return el;
    });

    triggerDownload('blob:http://example/abc', 'report.pdf');

    expect(created).toHaveLength(1);
    const anchor = created[0];
    expect(anchor.href).toBe('blob:http://example/abc');
    expect(anchor.download).toBe('report.pdf');
    expect(anchor.click).toHaveBeenCalledOnce();
    expect(document.body.contains(anchor)).toBe(false);
  });
});
