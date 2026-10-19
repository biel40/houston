export interface Levels {
  high: number
  critical: number
}

export const LOAD_LEVELS: Levels = { high: 70, critical: 90 }
export const CPU_TEMP_LEVELS: Levels = { high: 75, critical: 90 }
export const GPU_TEMP_LEVELS: Levels = { high: 75, critical: 85 }

/** Width of the band below `high` where the color starts blending towards the "high" color. */
const RAMP = 10

export interface LevelColorOptions {
  /** CSS variable used below the ramp. */
  normal?: string
  /**
   * Fade through the neutral color before turning amber. Needed for chromatic normals (brand
   * colors): blending blue straight into amber passes through green.
   */
  viaNeutral?: boolean
}

/**
 * Color for a reading: normal below the ramp, blending into "high" across the band just below
 * `high`, then high → critical. Fed with tweened values, so the color shifts gradually.
 */
export function levelColor(
  value: number | null,
  levels: Levels,
  { normal = '--level-normal', viaNeutral = false }: LevelColorOptions = {},
): string {
  if (value == null || value < levels.high - RAMP) return `var(${normal})`
  if (value < levels.high) {
    const t = (value - (levels.high - RAMP)) / RAMP
    if (!viaNeutral) return mix(normal, '--level-high', t)
    return t < 0.5 ? mix(normal, '--level-normal', t * 2) : mix('--level-normal', '--level-high', (t - 0.5) * 2)
  }
  return mix('--level-high', '--level-critical', (value - levels.high) / (levels.critical - levels.high))
}

function mix(from: string, to: string, t: number): string {
  // Whole percents keep the string stable between frames, so styles are only recomputed on real change.
  const percent = Math.round(Math.min(Math.max(t, 0), 1) * 100)
  return `color-mix(in oklch, var(${to}) ${percent}%, var(${from}))`
}
