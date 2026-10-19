<script lang="ts">
  interface Props {
    /** Percentages (0–100), oldest first. */
    values: readonly number[]
    capacity: number
    color: string
  }

  let { values, capacity, color }: Props = $props()

  // Newest sample pinned to the right edge; the line grows leftwards until the window is full.
  const coordinates = $derived(
    values.map((v, i) => `${capacity - values.length + i},${100 - Math.min(Math.max(v, 0), 100)}`),
  )
  const line = $derived(coordinates.join(' '))
  const area = $derived(
    values.length > 1 ? `${capacity - values.length},100 ${line} ${capacity - 1},100` : '',
  )
</script>

<svg viewBox="0 0 {capacity - 1} 100" preserveAspectRatio="none" aria-hidden="true">
  {#if values.length > 1}
    <polygon points={area} style:fill={color} />
    <polyline points={line} style:stroke={color} />
  {/if}
</svg>

<style>
  svg {
    display: block;
    width: 100%;
    height: 22px;
    overflow: visible;
  }
  polygon {
    opacity: 0.14;
  }
  polyline {
    fill: none;
    stroke-width: 1.25;
    stroke-linejoin: round;
    vector-effect: non-scaling-stroke;
  }
</style>
