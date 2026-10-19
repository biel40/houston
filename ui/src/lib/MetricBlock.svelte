<script lang="ts">
  import type { Snippet } from 'svelte'
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
    /** StatRow elements; rendered inside a <dl>. */
    children: Snippet
  }

  let { kind, name, vendor, load, history, historyCapacity, children }: Props = $props()

  const fullName = $derived(cleanName(name))
  const displayName = $derived(withoutVendor(fullName, vendor))
</script>

<!-- data-vendor selects the brand colors (app.css); unknown vendors fall back to neutral. -->
<section data-vendor={vendor ?? 'unknown'}>
  <header>
    <HardwareIcon {kind} />
    <span class="title">{kind.toUpperCase()}</span>
    <span class="name" title={fullName}>{displayName}</span>
    <VendorLogo {vendor} />
  </header>
  <div class="body">
    <!-- Matches the staggered rise in the styles below. -->
    <Gauge value={load} label="uso" introDelay={kind === 'gpu' ? 490 : 350} />
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
  section:nth-of-type(2) {
    animation-delay: 140ms;
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
