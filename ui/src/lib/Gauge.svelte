<script lang="ts">
  import { LOAD_LEVELS, levelColor } from './levels'
  import { Smoothed } from './smoothed.svelte'

  interface Props {
    value: number | null | undefined
    label: string
  }

  let { value, label }: Props = $props()

  const smooth = new Smoothed(() => value)

  const RADIUS = 40
  const CIRCUMFERENCE = 2 * Math.PI * RADIUS
  // 270° arc, open at the bottom where the label sits.
  const SWEEP = 0.75 * CIRCUMFERENCE

  const fraction = $derived(Math.min(Math.max((smooth.current ?? 0) / 100, 0), 1))
  // Brand color at rest (--brand comes from the enclosing block), shifting to amber and red under load.
  const color = $derived(levelColor(smooth.current, LOAD_LEVELS, { normal: '--brand', viaNeutral: true }))
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
        style:opacity={fraction > 0.004 ? 1 : 0}
      />
    </g>
  </svg>
  <div class="readout">
    {#if smooth.current == null}
      <span class="number">—</span>
    {:else}
      <span class="number">{Math.round(smooth.current)}</span><span class="unit">%</span>
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
  }
  circle {
    fill: none;
    stroke-width: 8;
    stroke-linecap: round;
  }
  .track {
    stroke: var(--track);
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
