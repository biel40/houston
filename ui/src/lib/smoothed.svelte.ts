import { untrack } from 'svelte'
import { cubicOut } from 'svelte/easing'
import { Tween, prefersReducedMotion } from 'svelte/motion'

/**
 * Shorter than the 1 s sampling interval, so each value settles before the next sample arrives.
 * Kept short on purpose: WebView2 renders in software here and animation frames are the main CPU cost.
 */
const DURATION = 400

/**
 * A nullable reading that glides between samples instead of jumping. The first value after a gap
 * (null) is shown immediately, and motion is skipped entirely under prefers-reduced-motion.
 * Must be created during component initialization.
 */
export class Smoothed {
  #tween = new Tween(0, { easing: cubicOut })
  #hasValue = $state(false)

  constructor(source: () => number | null | undefined) {
    $effect(() => {
      const value = source()
      if (value == null) {
        this.#hasValue = false
        return
      }
      const instant = untrack(() => !this.#hasValue) || prefersReducedMotion.current
      this.#tween.set(value, { duration: instant ? 0 : DURATION })
      this.#hasValue = true
    })
  }

  get current(): number | null {
    return this.#hasValue ? this.#tween.current : null
  }
}
