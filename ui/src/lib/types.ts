// Mirrors the host's versioned data contract (see AGENTS.md). Any field may be null.

/** Known values are 'intel', 'amd' and 'nvidia'; anything else falls back to neutral colors. */
export type Vendor = string | null

export interface CpuSnapshot {
  name: string
  vendor?: Vendor
  load: number | null
  temp: number | null
  clockMhz: number | null
  powerW: number | null
}

export interface GpuSnapshot {
  name: string
  vendor?: Vendor
  load: number | null
  temp: number | null
  clockMhz: number | null
  vramUsedMb: number | null
  vramTotalMb: number | null
  powerW: number | null
  fanRpm: number | null
}

export interface SensorSnapshot {
  v: number
  t: number
  cpu: CpuSnapshot | null
  gpu: GpuSnapshot | null
}
