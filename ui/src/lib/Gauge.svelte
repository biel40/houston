<script lang="ts">
  import { cubicInOut, cubicOut } from 'svelte/easing'
  import { Tween, prefersReducedMotion } from 'svelte/motion'
  import { LOAD_LEVELS, levelColor } from './levels'
  import { Smoothed } from './smoothed.svelte'

  interface Props {
    value: number | null | undefined
    label: string
    /** Start of the launch self-test, so stacked gauges can sweep one after another. */
    introDelay?: number
  }

  let { value, label, introDelay = 0 }: Props = $props()

  const smooth = new Smoothed(() => value)

  // Launch self-test, like a car dashboard: the needle sweeps to full and settles on the reading.
  const sweep = new Tween(0)
  let intro = $state(!prefersReducedMotion.current)

  $effect(() => {
    if (!intro) return
    let cancelled = false
    const timer = setTimeout(async () => {
      await sweep.set(100, { duration: 700, easing: cubicInOut })
      if (cancelled) return
      await sweep.set(smooth.current ?? 0, { duration: 600, easing: cubicOut })
      if (!cancelled) intro = false
    }, introDelay)
    return () => {
      cancelled = true
      clearTimeout(timer)
    }
  })

  const RADIUS = 40
  const CIRCUMFERENCE = 2 * Math.PI * RADIUS
  // 270° arc, open at the bottom where the label sits.
  const SWEEP = 0.75 * CIRCUMFERENCE

  const shown = $derived(intro ? sweep.current : smooth.current)
  const fraction = $derived(Math.min(Math.max((shown ?? 0) / 100, 0), 1))
  // Brand color at rest (--brand comes from the enclosing block), shifting to amber and red under load.
  const color = $derived(levelColor(shown, LOAD_LEVELS, { normal: '--brand', viaNeutral: true }))
  const visible = $derived(fraction > 0.004)
</script>

<div
  class="gauge"
  role="meter"
  aria-label={label}
  aria-valuemin={0}
  aria-valuemax={100}
  aria-valuenow={value ?? undefined}
>
  <svg viewBox="0 0 100 100" aria-hidden="true">
    <g transform="rotate(135 50 50)">
      <circle class="track" cx="50" cy="50" r={RADIUS} stroke-dasharray="{SWEEP} {CIRCUMFERENCE}" />
      <!-- Hidden at zero: a round cap on an empty dash would still draw a dot. -->
      <circle
        class="fill"
        cx="50"
        cy="50"
        r={RADIUS}
        stroke-dasharray="{SWEEP * fraction} {CIRCUMFERENCE}"
        style:stroke={color}
        style:opacity={visible ? 1 : 0}
      />
      <!-- Comet head riding the tip of the arc. -->
      <circle
        class="tip"
        cx={50 + RADIUS}
        cy="50"
        r="2.6"
        transform="rotate({270 * fraction} 50 50)"
        style:--glow={color}
        style:opacity={visible ? 1 : 0}
      />
    </g>
  </svg>
  <div class="readout">
    {#if shown == null}
      <span class="number">—</span>
    {:else}
      <span class="number">{Math.round(shown)}</span><span class="unit">%</span>
    {/if}
  </div>
  <span class="label">{label}</span>
</div>

<style>
  .gauge {
    position: relative;
    width: 76px;
    height: 76px;
    flex: none;
  }
  svg {
    width: 100%;
    height: 100%;
    overflow: visible;
  }
  circle {
    fill: none;
    stroke-width: 8;
    stroke-linecap: round;
  }
  .track {
    stroke: var(--track);
  }
  .tip {
    fill: rgb(255 255 255 / 0.95);
    stroke: none;
    filter: drop-shadow(0 0 2.5px var(--glow)) drop-shadow(0 0 1px var(--glow));
  }
  .readout {
    position: absolute;
    inset: 0;
    display: flex;
    align-items: baseline;
    justify-content: center;
    padding-top: 24px;
    font-variant-numeric: tabular-nums;
  }
  .number {
    font-size: 22px;
    font-weight: 600;
    line-height: 1;
  }
  .unit {
    margin-left: 1px;
    font-size: 11px;
    color: var(--text-secondary);
  }
  .label {
    position: absolute;
    left: 0;
    right: 0;
    bottom: 4px;
    text-align: center;
    font-size: 10px;
    color: var(--text-secondary);
  }
</style>
