<script lang="ts">
  import { send } from './bridge'

  interface Props {
    x: number
    y: number
    topmost: boolean
    onclose: () => void
  }

  let { x, y, topmost, onclose }: Props = $props()

  const margin = 4
  let width = $state(0)
  let height = $state(0)
  // Keep the menu inside the widget: the page cannot draw outside its own window.
  const left = $derived(Math.max(margin, Math.min(x, window.innerWidth - width - margin)))
  const top = $derived(Math.max(margin, Math.min(y, window.innerHeight - height - margin)))

  // The widget never takes focus and WebView2 fires spurious mouseleave events, so the host watches
  // for clicks outside the window while the menu is open and answers with 'dismissMenu'.
  $effect(() => {
    send({ type: 'menu', open: true })
    return () => send({ type: 'menu', open: false })
  })

  function toggleTopmost() {
    send({ type: 'setTopmost', value: !topmost })
    onclose()
  }
</script>

<div
  class="menu"
  role="menu"
  tabindex="-1"
  style:left="{left}px"
  style:top="{top}px"
  bind:offsetWidth={width}
  bind:offsetHeight={height}
  onpointerdown={(e) => e.stopPropagation()}
>
  <button role="menuitemcheckbox" aria-checked={topmost} onclick={toggleTopmost}>
    <svg class="check" class:visible={topmost} viewBox="0 0 16 16" aria-hidden="true">
      <path d="M3 8.5l3 3 7-7" />
    </svg>
    Siempre visible
  </button>
  <div class="separator" role="separator"></div>
  <button role="menuitem" onclick={() => send({ type: 'close' })}>
    <span class="check"></span>
    Cerrar
  </button>
</div>

<style>
  .menu {
    position: fixed;
    z-index: 10;
    min-width: 150px;
    padding: 4px;
    border-radius: 8px;
    border: 1px solid var(--border);
    background: var(--menu-surface);
    box-shadow: 0 4px 12px rgb(0 0 0 / 0.25);
    animation: pop 120ms ease-out;
  }
  button {
    display: flex;
    align-items: center;
    gap: 8px;
    width: 100%;
    height: 28px;
    padding: 0 10px 0 6px;
    border: none;
    border-radius: 4px;
    background: transparent;
    color: var(--text);
    font: inherit;
    text-align: left;
    cursor: default;
  }
  button:hover {
    background: var(--hover);
  }
  .check {
    width: 14px;
    height: 14px;
    flex: none;
    fill: none;
    stroke: currentColor;
    stroke-width: 1.6;
    stroke-linecap: round;
    stroke-linejoin: round;
    visibility: hidden;
  }
  .check.visible {
    visibility: visible;
  }
  .separator {
    height: 1px;
    margin: 4px 2px;
    background: var(--border);
  }
  @keyframes pop {
    from {
      opacity: 0;
      transform: translateY(-4px);
    }
  }
  @media (prefers-reduced-motion: reduce) {
    .menu {
      animation: none;
    }
  }
</style>
