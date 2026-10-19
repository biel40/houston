import type { SensorSnapshot } from './types'

export type HostMessage =
  | { type: 'sensors'; data: SensorSnapshot }
  | { type: 'window'; topmost: boolean }
  | { type: 'dismissMenu' }

/** Win32 WMSZ_* values: left, right, top, top-left, top-right, bottom, bottom-left, bottom-right. */
export type ResizeEdge = 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8

export type UiMessage =
  | { type: 'ready' }
  | { type: 'drag' }
  | { type: 'close' }
  | { type: 'minimize' }
  | { type: 'resize'; edge: ResizeEdge }
  | { type: 'setTopmost'; value: boolean }
  | { type: 'menu'; open: boolean }
  | { type: 'size'; width: number; height: number }

// Absent when the page is opened in a regular browser; every call then becomes a no-op.
const webview = window.chrome?.webview

export function send(message: UiMessage): void {
  webview?.postMessage(message)
}

export function onHostMessage(handler: (message: HostMessage) => void): () => void {
  if (!webview) return () => {}
  const listener = (event: MessageEvent) => handler(event.data as HostMessage)
  webview.addEventListener('message', listener)
  return () => webview.removeEventListener('message', listener)
}
