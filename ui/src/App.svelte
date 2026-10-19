<script lang="ts">
  import { onHostMessage, send } from './lib/bridge'
  import ContextMenu from './lib/ContextMenu.svelte'
  import { formatMemory } from './lib/format'
  import { connectHost, HISTORY_CAPACITY, host } from './lib/host.svelte'
  import { CPU_TEMP_LEVELS, GPU_TEMP_LEVELS } from './lib/levels'
  import MetricBlock from './lib/MetricBlock.svelte'
  import ResizeHandles from './lib/ResizeHandles.svelte'
  import Starfield from './lib/Starfield.svelte'
  import StatRow from './lib/StatRow.svelte'
  import WindowControls from './lib/WindowControls.svelte'
  import saturnV from './assets/saturn-v.svg'

  // The content is laid out at this width and scaled to whatever size the window has.
  const DESIGN_WIDTH = 300

  let menu = $state<{ x: number; y: number } | null>(null)
  let content = $state<HTMLElement>()
  let designHeight = $state(0)
  let viewWidth = $state(0)
  let viewHeight = $state(0)

  $effect(() => connectHost())
  $effect(() =>
    onHostMessage((message) => {
      if (message.type === 'dismissMenu') menu = null
    }),
  )

  // The layout decides the window's aspect ratio: report the natural size (at scale 1) and the host
  // keeps the window proportional to it while the user resizes.
  $effect(() => {
    if (!content) return
    const observer = new ResizeObserver(([entry]) => {
      designHeight = Math.ceil(entry.borderBoxSize[0].blockSize)
      send({ type: 'size', width: DESIGN_WIDTH, height: designHeight })
    })
    observer.observe(content)
    return () => observer.disconnect()
  })

  // The host keeps the window at the content's aspect ratio, so the two ratios match; taking the smaller
  // one and centering only matters for the instant before the host catches up.
  const scale = $derived(designHeight && viewWidth ? Math.min(viewWidth / DESIGN_WIDTH, viewHeight / designHeight) : 1)
  const offsetX = $derived((viewWidth - DESIGN_WIDTH * scale) / 2)
  const offsetY = $derived((viewHeight - designHeight * scale) / 2)

  const cpu = $derived(host.snapshot?.cpu)
  const gpu = $derived(host.snapshot?.gpu)

  function onPointerDown(e: PointerEvent) {
    if (e.button !== 0) return
    if (menu) {
      menu = null
      return
    }
    // Buttons and resize handles handle their own presses.
    if ((e.target as Element | null)?.closest('[data-nodrag]')) return
    send({ type: 'drag' })
  }

  function onContextMenu(e: MouseEvent) {
    e.preventDefault()
    menu = { x: e.clientX, y: e.clientY }
  }
</script>

<!-- On window, not on <main>: after WebView2's spurious mouseleave the event target can be <html>. -->
<svelte:window
  onpointerdown={onPointerDown}
  oncontextmenu={onContextMenu}
  bind:innerWidth={viewWidth}
  bind:innerHeight={viewHeight}
/>

<main class="card">
  <Starfield />
  <div
    class="content"
    bind:this={content}
    style:transform="translate({offsetX}px, {offsetY}px) scale({scale})"
  >
    <header class="brand">
      <img src={saturnV} alt="" width="22" height="22" />
      <span>Houston</span>
      <WindowControls />
    </header>

    <MetricBlock
      kind="cpu"
      name={cpu?.name}
      vendor={cpu?.vendor}
      load={cpu?.load}
      history={host.cpuHistory}
      historyCapacity={HISTORY_CAPACITY}
    >
      <StatRow label="Temperatura" value={cpu?.temp} unit="°C" levels={CPU_TEMP_LEVELS} />
      <StatRow label="Frecuencia" value={cpu?.clockMhz} unit="MHz" />
      <StatRow label="Potencia" value={cpu?.powerW} unit="W" digits={1} />
    </MetricBlock>

    <MetricBlock
      kind="gpu"
      name={gpu?.name}
      vendor={gpu?.vendor}
      load={gpu?.load}
      history={host.gpuHistory}
      historyCapacity={HISTORY_CAPACITY}
    >
      <StatRow label="Temperatura" value={gpu?.temp} unit="°C" levels={GPU_TEMP_LEVELS} />
      <StatRow label="Frecuencia" value={gpu?.clockMhz} unit="MHz" />
      <StatRow label="Memoria" value={gpu?.vramUsedMb} format={(used) => formatMemory(used, gpu?.vramTotalMb)} />
      <StatRow label="Potencia" value={gpu?.powerW} unit="W" digits={1} />
      <StatRow label="Ventilador" value={gpu?.fanRpm} unit="RPM" />
    </MetricBlock>
  </div>
</main>

<ResizeHandles />

{#if menu}
  <ContextMenu x={menu.x} y={menu.y} topmost={host.topmost} onclose={() => (menu = null)} />
{/if}

<style>
  .card {
    position: fixed;
    inset: 0;
    border-radius: 8px;
    overflow: hidden;
    color: var(--text);
    user-select: none;
    cursor: default;
  }
  /* Drawn over the content so the 1px border never overlaps its padding. */
  .card::after {
    content: '';
    position: absolute;
    inset: 0;
    z-index: 2;
    border: 1px solid var(--border);
    border-radius: inherit;
    pointer-events: none;
  }
  .content {
    position: absolute;
    top: 0;
    left: 0;
    z-index: 1;
    box-sizing: border-box;
    width: 300px;
    padding: 12px 14px 10px;
    transform-origin: 0 0;
  }
  .brand {
    display: flex;
    align-items: center;
    gap: 8px;
    margin-bottom: 10px;
    font-size: 13px;
    font-weight: 600;
    letter-spacing: 0.04em;
  }
  .brand img {
    display: block;
    border-radius: 5px;
  }
  .content :global(section + section) {
    margin-top: 8px;
  }
</style>
