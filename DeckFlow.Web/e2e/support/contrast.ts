import type { Locator } from '@playwright/test';

export interface RgbColor {
  r: number;
  g: number;
  b: number;
}

interface RgbaColor extends RgbColor {
  a: number;
}

const RGB_PATTERN =
  /^rgba?\(\s*(?<r>\d{1,3}(?:\.\d+)?)\s*,\s*(?<g>\d{1,3}(?:\.\d+)?)\s*,\s*(?<b>\d{1,3}(?:\.\d+)?)(?:\s*,\s*(?<a>\d*\.?\d+))?\s*\)$/i;
const HEX_PATTERN = /^#(?<hex>[0-9a-f]{3,8})$/i;

function clampChannel(value: number): number {
  return Math.min(255, Math.max(0, Math.round(value)));
}

function clampAlpha(value: number): number {
  return Math.min(1, Math.max(0, value));
}

function parseCssColorWithAlpha(input: string): RgbaColor {
  const trimmed = input.trim();
  if (trimmed.length === 0) {
    throw new Error('Cannot parse an empty CSS color');
  }

  if (/^transparent$/i.test(trimmed)) {
    return { r: 0, g: 0, b: 0, a: 0 };
  }

  const rgbMatch = trimmed.match(RGB_PATTERN);
  if (rgbMatch?.groups) {
    return {
      r: clampChannel(Number(rgbMatch.groups.r)),
      g: clampChannel(Number(rgbMatch.groups.g)),
      b: clampChannel(Number(rgbMatch.groups.b)),
      a: clampAlpha(rgbMatch.groups.a === undefined ? 1 : Number(rgbMatch.groups.a)),
    };
  }

  const hexMatch = trimmed.match(HEX_PATTERN);
  if (hexMatch?.groups?.hex) {
    const { hex } = hexMatch.groups;
    if (hex.length === 3 || hex.length === 4) {
      const [r, g, b, a = 'f'] = hex.split('');
      return {
        r: clampChannel(Number.parseInt(`${r}${r}`, 16)),
        g: clampChannel(Number.parseInt(`${g}${g}`, 16)),
        b: clampChannel(Number.parseInt(`${b}${b}`, 16)),
        a: clampAlpha(Number.parseInt(`${a}${a}`, 16) / 255),
      };
    }

    if (hex.length === 6 || hex.length === 8) {
      return {
        r: clampChannel(Number.parseInt(hex.slice(0, 2), 16)),
        g: clampChannel(Number.parseInt(hex.slice(2, 4), 16)),
        b: clampChannel(Number.parseInt(hex.slice(4, 6), 16)),
        a: clampAlpha(hex.length === 8 ? Number.parseInt(hex.slice(6, 8), 16) / 255 : 1),
      };
    }
  }

  throw new Error(`Unsupported CSS color: ${input}`);
}

function compositeColors(foreground: RgbaColor, background: RgbaColor): RgbaColor {
  const alpha = foreground.a + (background.a * (1 - foreground.a));
  if (alpha <= 0) {
    return { r: 255, g: 255, b: 255, a: 0 };
  }

  return {
    r: clampChannel(((foreground.r * foreground.a) + (background.r * background.a * (1 - foreground.a))) / alpha),
    g: clampChannel(((foreground.g * foreground.a) + (background.g * background.a * (1 - foreground.a))) / alpha),
    b: clampChannel(((foreground.b * foreground.a) + (background.b * background.a * (1 - foreground.a))) / alpha),
    a: clampAlpha(alpha),
  };
}

export function parseCssColor(input: string): RgbColor {
  const { r, g, b } = parseCssColorWithAlpha(input);
  return { r, g, b };
}

export function relativeLuminance(color: RgbColor): number {
  const toLinear = (channel: number): number => {
    const normalized = channel / 255;
    return normalized <= 0.04045 ? normalized / 12.92 : ((normalized + 0.055) / 1.055) ** 2.4;
  };

  return (0.2126 * toLinear(color.r)) + (0.7152 * toLinear(color.g)) + (0.0722 * toLinear(color.b));
}

export function contrastRatio(foreground: RgbColor, background: RgbColor): number {
  const lighter = Math.max(relativeLuminance(foreground), relativeLuminance(background));
  const darker = Math.min(relativeLuminance(foreground), relativeLuminance(background));
  return Math.min(21, Math.max(1, (lighter + 0.05) / (darker + 0.05)));
}

export async function effectiveBackgroundColor(locator: Locator): Promise<RgbColor> {
  const resolved = await locator.evaluate((element) => {
    type BrowserRgbaColor = { r: number; g: number; b: number; a: number };

    const rgbPattern =
      /^rgba?\(\s*(\d{1,3}(?:\.\d+)?)\s*,\s*(\d{1,3}(?:\.\d+)?)\s*,\s*(\d{1,3}(?:\.\d+)?)(?:\s*,\s*(\d*\.?\d+))?\s*\)$/i;
    const hexPattern = /^#([0-9a-f]{3,8})$/i;

    const clampChannelValue = (value: number): number => Math.min(255, Math.max(0, Math.round(value)));
    const clampAlphaValue = (value: number): number => Math.min(1, Math.max(0, value));

    const parseColor = (input: string): BrowserRgbaColor => {
      const trimmed = input.trim();
      if (/^transparent$/i.test(trimmed)) {
        return { r: 0, g: 0, b: 0, a: 0 };
      }

      const rgbMatch = trimmed.match(rgbPattern);
      if (rgbMatch) {
        return {
          r: clampChannelValue(Number(rgbMatch[1])),
          g: clampChannelValue(Number(rgbMatch[2])),
          b: clampChannelValue(Number(rgbMatch[3])),
          a: clampAlphaValue(rgbMatch[4] === undefined ? 1 : Number(rgbMatch[4])),
        };
      }

      const hexMatch = trimmed.match(hexPattern);
      if (hexMatch) {
        const [, hex] = hexMatch;
        if (hex.length === 3 || hex.length === 4) {
          const [r, g, b, a = 'f'] = hex.split('');
          return {
            r: clampChannelValue(Number.parseInt(`${r}${r}`, 16)),
            g: clampChannelValue(Number.parseInt(`${g}${g}`, 16)),
            b: clampChannelValue(Number.parseInt(`${b}${b}`, 16)),
            a: clampAlphaValue(Number.parseInt(`${a}${a}`, 16) / 255),
          };
        }

        if (hex.length === 6 || hex.length === 8) {
          return {
            r: clampChannelValue(Number.parseInt(hex.slice(0, 2), 16)),
            g: clampChannelValue(Number.parseInt(hex.slice(2, 4), 16)),
            b: clampChannelValue(Number.parseInt(hex.slice(4, 6), 16)),
            a: clampAlphaValue(hex.length === 8 ? Number.parseInt(hex.slice(6, 8), 16) / 255 : 1),
          };
        }
      }

      throw new Error(`Unsupported CSS color: ${input}`);
    };

    const composite = (foreground: BrowserRgbaColor, background: BrowserRgbaColor): BrowserRgbaColor => {
      const alpha = foreground.a + (background.a * (1 - foreground.a));
      if (alpha <= 0) {
        return { r: 255, g: 255, b: 255, a: 0 };
      }

      return {
        r: clampChannelValue(((foreground.r * foreground.a) + (background.r * background.a * (1 - foreground.a))) / alpha),
        g: clampChannelValue(((foreground.g * foreground.a) + (background.g * background.a * (1 - foreground.a))) / alpha),
        b: clampChannelValue(((foreground.b * foreground.a) + (background.b * background.a * (1 - foreground.a))) / alpha),
        a: clampAlphaValue(alpha),
      };
    };

    const fallback = { r: 255, g: 255, b: 255, a: 1 };
    let effective: BrowserRgbaColor | null = null;
    let current: HTMLElement | null = element as HTMLElement;

    while (current !== null) {
      const background = parseColor(getComputedStyle(current).backgroundColor);
      if (background.a > 0) {
        effective = effective === null ? background : composite(effective, background);
        if (effective.a >= 0.999) {
          break;
        }
      }

      current = current.parentElement;
    }

    if (effective === null) {
      effective = fallback;
    } else if (effective.a < 0.999) {
      effective = composite(effective, fallback);
    }

    return { r: effective.r, g: effective.g, b: effective.b };
  });

  return resolved;
}

export async function resolveContrast(locator: Locator): Promise<{
  foreground: RgbColor;
  background: RgbColor;
  ratio: number;
}> {
  const [foregroundCss, background] = await Promise.all([
    locator.evaluate((element) => getComputedStyle(element as HTMLElement).color),
    effectiveBackgroundColor(locator),
  ]);
  const foreground = parseCssColor(foregroundCss);

  return {
    foreground,
    background,
    ratio: contrastRatio(foreground, background),
  };
}
