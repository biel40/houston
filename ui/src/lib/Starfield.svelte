<script lang="ts">
  // Static starfield behind the widget. Stars come from a seeded generator so the sky is identical on
  // every launch, and nothing animates: a twinkling loop would keep the renderer busy for decoration.
  const WIDTH = 300
  const HEIGHT = 340
  const COUNT = 46

  function mulberry32(seed: number): () => number {
    return () => {
      seed = (seed + 0x6d2b79f5) | 0
      let t = Math.imul(seed ^ (seed >>> 15), 1 | seed)
      t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t
      return ((t ^ (t >>> 14)) >>> 0) / 4294967296
    }
  }

  const random = mulberry32(1969)
  // Random stars stay small and dim so none can be mistaken for punctuation next to a value;
  // the few bright ones are placed by hand where there is no text.
  const stars = [
    ...Array.from({ length: COUNT }, () => ({
      x: random() * WIDTH,
      y: random() * HEIGHT,
      r: 0.3 + random() * 0.4,
      opacity: 0.18 + random() * 0.3,
    })),
    { x: 168, y: 16, r: 1.1, opacity: 0.85 },
    { x: 262, y: 30, r: 0.9, opacity: 0.7 },
    { x: 210, y: 8, r: 0.8, opacity: 0.6 },
  ]
</script>

<div class="sky" aria-hidden="true">
  <svg viewBox="0 0 {WIDTH} {HEIGHT}" preserveAspectRatio="xMidYMid slice">
    {#each stars as star, i (i)}
      <circle cx={star.x} cy={star.y} r={star.r} opacity={star.opacity} />
    {/each}
  </svg>
</div>

<style>
  .sky {
    position: absolute;
    inset: 0;
    z-index: 0;
    pointer-events: none;
    background: var(--card-background);
    /* Own compositing layer: WebView2 renders in software here, and without this every animation
       frame would repaint the sky and stars underneath the gauges. */
    will-change: transform;
  }
  svg {
    display: block;
    width: 100%;
    height: 100%;
  }
  circle {
    fill: var(--star);
  }
</style>
