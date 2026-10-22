<script lang="ts">
  import { flushSync } from 'svelte'
  import { flip } from 'svelte/animate'
  import { prefersReducedMotion } from 'svelte/motion'
  import { fade } from 'svelte/transition'
  import { onHostMessage, send } from './lib/bridge'
  import ContextMenu from './lib/ContextMenu.svelte'
  import { formatMemory } from './lib/format'
  import { connectHost, HISTORY_CAPACITY, host } from './lib/host.svelte'
  import { layout, type CardId } from './lib/layout.svelte'
  import { CPU_TEMP_LEVELS, GPU_TEMP_LEVELS } from './lib/levels'
  import MetricBlock from './lib/MetricBlock.svelte'
  import ResizeHandles from './lib/ResizeHandles.svelte'
  import Starfield from './lib/Starfield.svelte'
  import StatRow from './lib/StatRow.svelte'
  import WindowControls from './lib/WindowControls.svelte'
  import saturnV from './assets/saturn-v.svg'

  // The content is laid out at this width and scaled to whatever size the window has.
  const DESIGN_WIDTH = 300
  // Vertical space between cards; keep in sync with `.slot + .slot` below.
  const CARD_GAP = 8

  interface Drag {
    id: CardId
    pointerId: number
    startY: number
    startTop: number
    /** Translation of the dragged card, in layout pixels (before the content is scaled). */
    offset: number
    startOrder: string
  }

  let menu = $state<{ x: number; y: number; align: 'start' | 'end' } | null>(null)
  let content = $state<HTMLElement>()
  let designHeight = $state(0)
  let viewWidth = $state(0)
  let viewHeight = $state(0)
  let drag = $state<Drag | null>(null)
  const slots: Partial<Record<CardId, HTMLElement>> = {}

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
      // Before the saved layout arrives only the header is rendered; reporting it would shrink the window.
      if (!layout.ready) return
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
  const reorderable = $derived(layout.visible.length > 1)

  function onPointerDown(e: PointerEvent) {
    if (e.button !== 0) return
    if (menu) {
      // The options button toggles the menu itself on click.
      if (!(e.target as Element | null)?.closest('[data-menu-anchor]')) menu = null
      return
    }
    // Buttons, card handles and resize handles handle their own presses.
    if ((e.target as Element | null)?.closest('[data-nodrag]')) return
    send({ type: 'drag' })
  }

  function onContextMenu(e: MouseEvent) {
    e.preventDefault()
    menu = { x: e.clientX, y: e.clientY, align: 'start' }
  }

  function toggleOptions(anchor: DOMRect) {
    menu = menu ? null : { x: anchor.right, y: anchor.bottom + 4, align: 'end' }
  }

  function slotAt(index: number): HTMLElement | undefined {
    const id = layout.visible[index]
    return id && slots[id]
  }

  function grab(id: CardId, e: PointerEvent) {
    const slot = slots[id]
    if (!slot) return
    drag = { id, pointerId: e.pointerId, startY: e.clientY, startTop: slot.offsetTop, offset: 0, startOrder: layout.order.join() }
    // Captured so the drag keeps tracking outside the window; the window listeners still receive the events.
    // Best effort: without it the drag still works while the pointer stays inside.
    try {
      ;(e.currentTarget as Element).setPointerCapture(e.pointerId)
    } catch {}
  }

  function onPointerMove(e: PointerEvent) {
    if (!drag || e.pointerId !== drag.pointerId) return
    const slot = slots[drag.id]
    if (!slot) return

    // How far the pointer travelled, minus how far the card's own slot has shifted through reorders.
    const travel = (e.clientY - drag.startY) / scale
    const offset = travel - (slot.offsetTop - drag.startTop)
    const index = layout.visible.indexOf(drag.id)
    const next = slotAt(index + 1)
    const previous = slotAt(index - 1)
    // Swap once an edge passes the neighbour's middle; the gap keeps it from swapping straight back.
    if (next && offset > next.offsetHeight / 2 + CARD_GAP) {
      layout.move(drag.id, index + 1)
      flushSync()
    } else if (previous && offset < -(previous.offsetHeight / 2 + CARD_GAP)) {
      layout.move(drag.id, index - 1)
      flushSync()
    }

    // Kept within the stack of cards so it never slides under the header or out of the window.
    const first = slotAt(0)
    const last = slotAt(layout.visible.length - 1)
    if (!first || !last) return
    const min = first.offsetTop - slot.offsetTop
    const max = last.offsetTop + last.offsetHeight - slot.offsetTop - slot.offsetHeight
    drag.offset = Math.min(Math.max(travel - (slot.offsetTop - drag.startTop), min), max)
  }

  function drop(e: PointerEvent) {
    if (!drag || e.pointerId !== drag.pointerId) return
    if (layout.order.join() !== drag.startOrder) layout.save()
    drag = null
  }

  function step(id: CardId, direction: -1 | 1) {
    const target = layout.visible.indexOf(id) + direction
    if (target < 0 || target >= layout.visible.length) return
    layout.move(id, target)
    layout.save()
    // Moving a node in the DOM can drop its focus; keep it on the handle for repeated presses.
    flushSync()
    slots[id]?.querySelector<HTMLElement>('[data-nodrag]')?.focus()
  }
</script>

<!-- On window, not on <main>: after WebView2's spurious mouseleave the event target can be <html>. -->
<svelte:window
  onpointerdown={onPointerDown}
  onpointermove={onPointerMove}
  onpointerup={drop}
  onpointercancel={drop}
  oncontextmenu={onContextMenu}
  bind:innerWidth={viewWidth}
  bind:innerHeight={viewHeight}
/>

<main class="card" class:maximized={host.maximized}>
  <Starfield />
  <div
    class="content"
    bind:this={content}
    style:transform="translate({offsetX}px, {offsetY}px) scale({scale})"
  >
    <header class="brand">
      <img src={saturnV} alt="" width="22" height="22" />
      <span>Houston</span>
      <WindowControls onoptions={toggleOptions} />
    </header>

    {#if layout.ready}
      {#each layout.visible as id, index (id)}
        {@const dragging = drag?.id === id}
        <!-- The dragged card follows the pointer; flip only animates the others into their new places. -->
        <div
          class="slot"
          class:dragging
          bind:this={slots[id]}
          style:transform={dragging ? `translateY(${drag?.offset ?? 0}px)` : undefined}
          animate:flip={{ duration: dragging || prefersReducedMotion.current ? 0 : 220 }}
          out:fade={{ duration: prefersReducedMotion.current ? 0 : 120 }}
        >
          {#if id === 'cpu'}
            <MetricBlock
              kind="cpu"
              name={cpu?.name}
              vendor={cpu?.vendor}
              load={cpu?.load}
              history={host.cpuHistory}
              historyCapacity={HISTORY_CAPACITY}
              {index}
              {dragging}
              ongrab={reorderable ? (e) => grab(id, e) : undefined}
              onstep={(direction) => step(id, direction)}
            >
              <StatRow label="Temperatura" value={cpu?.temp} unit="°C" levels={CPU_TEMP_LEVELS} />
              <StatRow label="Frecuencia" value={cpu?.clockMhz} unit="MHz" />
              <StatRow label="Potencia" value={cpu?.powerW} unit="W" digits={1} />
            </MetricBlock>
          {:else}
            <MetricBlock
              kind="gpu"
              name={gpu?.name}
              vendor={gpu?.vendor}
              load={gpu?.load}
              history={host.gpuHistory}
              historyCapacity={HISTORY_CAPACITY}
              {index}
              {dragging}
              ongrab={reorderable ? (e) => grab(id, e) : undefined}
              onstep={(direction) => step(id, direction)}
            >
              <StatRow label="Temperatura" value={gpu?.temp} unit="°C" levels={GPU_TEMP_LEVELS} />
              <StatRow label="Frecuencia" value={gpu?.clockMhz} unit="MHz" />
              <StatRow label="Memoria" value={gpu?.vramUsedMb} format={(used) => formatMemory(used, gpu?.vramTotalMb)} />
              <StatRow label="Potencia" value={gpu?.powerW} unit="W" digits={1} />
              <StatRow label="Ventilador" value={gpu?.fanRpm} unit="RPM" />
            </MetricBlock>
          {/if}
        </div>
      {/each}
    {/if}
  </div>
</main>

{#if !host.maximized}
  <ResizeHandles />
{/if}

{#if menu}
  <ContextMenu x={menu.x} y={menu.y} align={menu.align} topmost={host.topmost} onclose={() => (menu = null)} />
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
  /* Maximized windows have square corners in Windows 11. */
  .card.maximized {
    border-radius: 0;
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
  .slot {
    position: relative;
    /* Settles back into its slot when dropped; no transition while it follows the pointer. */
    transition: transform 180ms cubic-bezier(0.2, 0.9, 0.3, 1);
  }
  .slot.dragging {
    z-index: 1;
    transition: none;
  }
  .slot + .slot {
    margin-top: 8px;
  }
  @media (prefers-reduced-motion: reduce) {
    .slot {
      transition: none;
    }
  }
</style>
