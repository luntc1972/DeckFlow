import { describe, expect, it } from 'vitest';
import { contrastRatio, parseCssColor, relativeLuminance } from '../e2e/support/contrast';

describe('cut-lab contrast math', () => {
  it('computes WCAG contrast bounds for black and white and identical colors', () => {
    expect(contrastRatio({ r: 0, g: 0, b: 0 }, { r: 255, g: 255, b: 255 })).toBeCloseTo(21, 1);
    expect(contrastRatio({ r: 45, g: 122, b: 79 }, { r: 45, g: 122, b: 79 })).toBe(1);
  });

  it('parses rgb() and rgba() CSS colors into rgb channels', () => {
    expect(parseCssColor('rgb(26,21,16)')).toEqual({ r: 26, g: 21, b: 16 });
    expect(parseCssColor('rgba(45,122,79,1)')).toEqual({ r: 45, g: 122, b: 79 });
  });

  it('returns the WCAG relative luminance bounds for black and white', () => {
    expect(relativeLuminance({ r: 0, g: 0, b: 0 })).toBe(0);
    expect(relativeLuminance({ r: 255, g: 255, b: 255 })).toBe(1);
  });
});
