// Minimal typing for the WebView2 bridge; only present when hosted by Houston.
interface Window {
  chrome?: {
    webview?: {
      postMessage(message: unknown): void
      addEventListener(type: 'message', listener: (event: MessageEvent) => void): void
      removeEventListener(type: 'message', listener: (event: MessageEvent) => void): void
    }
  }
}
