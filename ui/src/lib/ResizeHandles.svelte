<script lang="ts">
  import { send, type ResizeEdge } from './bridge'

  // Win32 WMSZ_* values, so the host can hand them straight to the system sizing loop.
  const handles: { edge: ResizeEdge; name: string }[] = [
    { edge: 1, name: 'left' },
    { edge: 2, name: 'right' },
    { edge: 3, name: 'top' },
    { edge: 4, name: 'top-left' },
    { edge: 5, name: 'top-right' },
    { edge: 6, name: 'bottom' },
    { edge: 7, name: 'bottom-left' },
    { edge: 8, name: 'bottom-right' },
  ]

  function onPointerDown(e: PointerEvent, edge: ResizeEdge) {
    if (e.button === 0) send({ type: 'resize', edge })
  }
</script>

<!-- Invisible strips along the window edge; data-nodrag keeps the press from also starting a drag. -->
{#each handles as { edge, name } (edge)}
  <div class="handle {name}" data-nodrag aria-hidden="true" onpointerdown={(e) => onPointerDown(e, edge)}></div>
{/each}

<style>
  .handle {
    position: fixed;
    z-index: 5;
  }
  .left,
  .right {
    top: 8px;
    bottom: 8px;
    width: 6px;
    cursor: ew-resize;
  }
  .top,
  .bottom {
    left: 8px;
    right: 8px;
    height: 6px;
    cursor: ns-resize;
  }
  .left { left: 0; }
  .right { right: 0; }
  .top { top: 0; }
  .bottom { bottom: 0; }
  .top-left,
  .top-right,
  .bottom-left,
  .bottom-right {
    width: 12px;
    height: 12px;
  }
  .top-left { top: 0; left: 0; cursor: nwse-resize; }
  .bottom-right { right: 0; bottom: 0; cursor: nwse-resize; }
  .top-right { top: 0; right: 0; cursor: nesw-resize; }
  .bottom-left { bottom: 0; left: 0; cursor: nesw-resize; }
</style>
