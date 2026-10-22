<script lang="ts">
  import { untrack, type Snippet } from 'svelte'
  import { cleanName, withoutVendor } from './format'
  import Gauge from './Gauge.svelte'
  import HardwareIcon from './HardwareIcon.svelte'
  import Sparkline from './Sparkline.svelte'
  import type { Vendor } from './types'
  import VendorLogo from './VendorLogo.svelte'

  interface Props {
    kind: 'cpu' | 'gpu'
    name: string | null | undefined
    vendor: Vendor | undefined
    load: number | null | undefined
    history: readonly number[]
    historyCapacity: number
    /** Position among the visible cards; staggers the launch animation. */
    index: number
    /** Lifted while the user drags it to a new position. */
    dragging?: boolean
    /** Present when the card can be reordered: the hardware icon becomes its drag handle. */
    ongrab?: (event: PointerEvent) => void
    /** Keyboard reordering from the handle: -1 moves up, 1 moves down. */
    onstep?: (direction: -1 | 1) => void
    /** StatRow elements; rendered inside a <dl>. */
    children: Snippet
  }

  let { kind, name, vendor, load, history, historyCapacity, index, dragging = false, ongrab, onstep, children }: Props =
    $props()

  const fullName = $derived(cleanName(name))
  const displayName = $derived(withoutVendor(fullName, vendor))

  // Read once: the stagger belongs to the launch, not to later reorders.
  const riseDelay = untrack(() => index) * 140

  function onHandleKeydown(e: KeyboardEvent) {
    if (e.key === 'ArrowUp' || e.key === 'ArrowDown') {
      e.preventDefault()
      onstep?.(e.key === 'ArrowUp' ? -1 : 1)
    }
  }
</script>

<!-- data-vendor selects the brand colors (app.css); unknown vendors fall back to neutral. -->
<section data-vendor={vendor ?? 'unknown'} class:dragging style:animation-delay="{riseDelay}ms">
  <header>
    {#if ongrab}
      <!-- data-nodrag: pressing the handle reorders the card instead of moving the window. -->
      <button
        class="handle"
        type="button"
        data-nodrag
        aria-label="Mover tarjeta {kind.toUpperCase()} (flechas arriba y abajo)"
        title="Arrastra para reordenar"
        onpointerdown={(e) => e.button === 0 && ongrab(e)}
        onkeydown={onHandleKeydown}
      >
        <HardwareIcon {kind} />
        <span class="grip" aria-hidden="true">
          <svg viewBox="0 0 16 16">
            <circle cx="6" cy="4" r="1.2" /><circle cx="10" cy="4" r="1.2" />
            <circle cx="6" cy="8" r="1.2" /><circle cx="10" cy="8" r="1.2" />
            <circle cx="6" cy="12" r="1.2" /><circle cx="10" cy="12" r="1.2" />
          </svg>
        </span>
      </button>
    {:else}
      <HardwareIcon {kind} />
    {/if}
    <span class="title">{kind.toUpperCase()}</span>
    <span class="name" title={fullName}>{displayName}</span>
    <VendorLogo {vendor} />
  </header>
  <div class="body">
    <!-- Matches the staggered rise in the styles below. -->
    <Gauge value={load} label="uso" introDelay={350 + riseDelay} />
    <dl>{@render children()}</dl>
  </div>
  <Sparkline values={history} capacity={historyCapacity} color="var(--brand)" />
</section>
<style>
  section {
    position: relative;
    isolation: isolate;
    margin: 0 -8px;
    padding: 8px 8px 6px;
    animation: rise 650ms cubic-bezier(0.2, 0.9, 0.3, 1.15) backwards;
  }
  /* Lifted: an opaque surface so the card it passes over does not show through. */
  section.dragging {
    border-radius: 8px;
    background: var(--card-background);
    box-shadow: 0 8px 24px rgb(0 0 0 / 0.35);
  }
  /* Launch: each block lifts off into place. */
  @keyframes rise {
    from {
      opacity: 0;
      transform: translateY(18px) scale(0.97);
    }
  }
  @media (prefers-reduced-motion: reduce) {
    section {
      animation: none;
    }
  }
  /* The brand glow lives on its own layer for the same reason as the starfield: the gauge animates
     on top of it, and a shared layer would repaint the gradient on every frame. */
  section::before {
    content: '';
    position: absolute;
    inset: 0;
    z-index: -1;
    border-radius: 8px;
    background: radial-gradient(120% 95% at 100% 0%, var(--brand-glow), transparent 72%);
    will-change: transform;
  }
  header {
    display: flex;
    align-items: center;
    gap: 8px;
    margin-bottom: 6px;
  }
  /* The icon turns into a grip while the pointer is over the card, so the handle costs no extra room. */
  .handle {
    position: relative;
    display: block;
    flex: none;
    padding: 0;
    border: none;
    border-radius: 6px;
    background: none;
    color: inherit;
    cursor: grab;
    touch-action: none;
  }
  .dragging .handle {
    cursor: grabbing;
  }
  .handle:focus-visible {
    outline: 2px solid var(--brand);
    outline-offset: 2px;
  }
  .grip {
    position: absolute;
    inset: 0;
    display: grid;
    place-items: center;
    border-radius: 6px;
    background: var(--brand);
    color: var(--on-brand);
    opacity: 0;
    transition: opacity 150ms ease-out;
  }
  .grip svg {
    width: 15px;
    height: 15px;
    fill: currentColor;
  }
  section:hover .grip,
  .handle:focus-visible .grip,
  .dragging .grip {
    opacity: 1;
  }
  @media (prefers-reduced-motion: reduce) {
    .grip {
      transition: none;
    }
  }
  .title {
    font-weight: 600;
    letter-spacing: 0.02em;
  }
  .name {
    min-width: 0;
    overflow: hidden;
    color: var(--text-secondary);
    white-space: nowrap;
    text-overflow: ellipsis;
  }
  .body {
    display: flex;
    align-items: center;
    gap: 14px;
    margin-bottom: 6px;
  }
  dl {
    flex: 1;
    display: grid;
    grid-template-columns: auto 1fr;
    gap: 2px 12px;
    margin: 0;
  }
</style>
