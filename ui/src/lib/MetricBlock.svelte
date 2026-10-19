<script lang="ts">
  import type { Snippet } from 'svelte'
  import { cleanName } from './format'
  import Gauge from './Gauge.svelte'
  import HardwareIcon from './HardwareIcon.svelte'
  import Sparkline from './Sparkline.svelte'
  import type { Vendor } from './types'

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

  const displayName = $derived(cleanName(name))
</script>

<!-- data-vendor selects the brand colors (app.css); unknown vendors fall back to neutral. -->
<section data-vendor={vendor ?? 'unknown'}>
  <header>
    <HardwareIcon {kind} />
    <span class="title">{kind.toUpperCase()}</span>
    <span class="name" title={displayName}>{displayName}</span>
  </header>
  <div class="body">
    <Gauge value={load} label="uso" />
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
