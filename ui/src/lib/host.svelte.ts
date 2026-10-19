import { onHostMessage, send } from './bridge'
import type { SensorSnapshot } from './types'

/** One sample per second, so this is the last minute of load. */
export const HISTORY_CAPACITY = 60

class HostState {
  snapshot = $state<SensorSnapshot | null>(null)
  topmost = $state(true)
  maximized = $state(false)
  // Replaced (not mutated) on every sample, so raw state is enough and avoids deep proxies.
  cpuHistory = $state.raw<number[]>([])
  gpuHistory = $state.raw<number[]>([])
}

export const host = new HostState()

function append(history: number[], value: number | null | undefined): number[] {
  if (value == null) return history
  const next = history.length >= HISTORY_CAPACITY ? history.slice(1) : history.slice()
  next.push(value)
  return next
}

/** Starts listening to the host and announces the UI is ready. Returns the disconnect function. */
export function connectHost(): () => void {
  const disconnect = onHostMessage((message) => {
    switch (message.type) {
      case 'sensors':
        host.snapshot = message.data
        host.cpuHistory = append(host.cpuHistory, message.data.cpu?.load)
        host.gpuHistory = append(host.gpuHistory, message.data.gpu?.load)
        break
      case 'window':
        host.topmost = message.topmost
        host.maximized = message.maximized
        break
    }
  })
  send({ type: 'ready' })
  return disconnect
}
