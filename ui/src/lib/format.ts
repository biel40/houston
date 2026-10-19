/** Formats a nullable reading; missing sensors render as an em dash. */
export function formatValue(value: number | null | undefined, unit: string, digits = 0): string {
  return value == null ? '—' : `${value.toFixed(digits)} ${unit}`
}

/** "1.5 / 8.0 GB", or MB when the total is under 1 GB. */
export function formatMemory(usedMb: number | null | undefined, totalMb: number | null | undefined): string {
  if (usedMb == null || totalMb == null) return '—'
  if (totalMb < 1024) return `${Math.round(usedMb)} / ${Math.round(totalMb)} MB`
  return `${(usedMb / 1024).toFixed(1)} / ${(totalMb / 1024).toFixed(1)} GB`
}

/** Drops trademark noise from vendor names: "Intel(R) Arc(TM) Graphics" → "Intel Arc Graphics". */
export function cleanName(name: string | null | undefined): string {
  if (!name) return '—'
  return name.replace(/\s*\((R|TM|C)\)/gi, '').replace(/\s+/g, ' ').trim()
}

/** "Intel Core Ultra 7 155H" → "Core Ultra 7 155H" when the vendor logo already says who made it. */
export function withoutVendor(name: string, vendor: string | null | undefined): string {
  const prefix = `${vendor} `.toLowerCase()
  if (!vendor || !name.toLowerCase().startsWith(prefix)) return name
  return name.slice(prefix.length).trim() || name
}
