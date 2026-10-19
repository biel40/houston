<script lang="ts">
  import { formatValue } from './format'
  import { type Levels, levelColor } from './levels'
  import { Smoothed } from './smoothed.svelte'

  interface Props {
    label: string
    value: number | null | undefined
    unit?: string
    digits?: number
    /** Colors the value as it approaches the high/critical thresholds. */
    levels?: Levels
    /** Custom text for compound readings (e.g. used / total memory). */
    format?: (value: number) => string
  }

  let { label, value, unit = '', digits = 0, levels, format }: Props = $props()

  const smooth = new Smoothed(() => value)

  const text = $derived(
    smooth.current != null && format ? format(smooth.current) : formatValue(smooth.current, unit, digits),
  )
  const color = $derived(levels ? levelColor(smooth.current, levels, { normal: '--text' }) : undefined)
</script>

<dt>{label}</dt>
<dd class:missing={smooth.current == null} style:color>{text}</dd>

<style>
  dt {
    color: var(--text-secondary);
  }
  dd {
    margin: 0;
    text-align: right;
    font-variant-numeric: tabular-nums;
    white-space: nowrap;
  }
  .missing {
    color: var(--text-secondary);
  }
</style>
