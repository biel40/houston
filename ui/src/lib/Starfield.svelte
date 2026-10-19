<script lang="ts">
  import { prefersReducedMotion } from 'svelte/motion'

  // Starfield behind the widget. Stars come from a seeded generator so the sky is identical on every
  // launch, and they do not twinkle: a looping animation would keep the renderer busy for decoration.
  // The only motion is an occasional shooting star, which costs a second of frames every ~15 s.
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

  interface Meteor {
    id: number
    /** Start point, as a percentage of the sky. */
    x: number
    y: number
    angle: number
  }

  let meteor = $state<Meteor | null>(null)

  $effect(() => {
    if (prefersReducedMotion.current) return
    let id = 0
    let timer: ReturnType<typeof setTimeout>
    const schedule = (delay: number) => {
      timer = setTimeout(() => {
        // A hidden window has nobody to impress.
        if (!document.hidden) {
          meteor = { id: ++id, x: 15 + Math.random() * 70, y: Math.random() * 35, angle: 20 + Math.random() * 25 }
        }
        schedule(10_000 + Math.random() * 12_000)
      }, delay)
    }
    schedule(2_500)
    return () => clearTimeout(timer)
  })
</script>

<div class="sky" aria-hidden="true">
  <svg viewBox="0 0 {WIDTH} {HEIGHT}" preserveAspectRatio="xMidYMid slice">
    {#each stars as star, i (i)}
      <circle cx={star.x} cy={star.y} r={star.r} opacity={star.opacity} />
    {/each}
  </svg>
  {#if meteor}
    {#key meteor.id}
      <div
        class="meteor"
        style:left="{meteor.x}%"
        style:top="{meteor.y}%"
        style:rotate="{meteor.angle}deg"
        onanimationend={() => (meteor = null)}
      ></div>
    {/key}
  {/if}
</div>

<style>
  .sky {
    position: absolute;
    inset: 0;
    z-index: 0;
    overflow: hidden;
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
  /* A bright head with a fading tail, flying along its own rotated x axis. */
  .meteor {
    position: absolute;
    width: 70px;
    height: 1.5px;
    border-radius: 1px;
    background: linear-gradient(90deg, transparent, var(--star));
    transform-origin: 0 50%;
    opacity: 0;
    will-change: transform, opacity;
    animation: shoot 1100ms cubic-bezier(0.4, 0, 0.7, 1);
  }
  .meteor::after {
    content: '';
    position: absolute;
    right: -1.5px;
    top: -1.25px;
    width: 4px;
    height: 4px;
    border-radius: 50%;
    background: var(--star);
    box-shadow: 0 0 6px 1px var(--star);
  }
  @keyframes shoot {
    0% {
      opacity: 0;
      transform: translateX(-70px) scaleX(0.3);
    }
    15% {
      opacity: 1;
    }
    70% {
      opacity: 0.9;
    }
    100% {
      opacity: 0;
      transform: translateX(200px) scaleX(1);
    }
  }
</style>
