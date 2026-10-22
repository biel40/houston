<script lang="ts">
  import { send } from './bridge'
  import { host } from './host.svelte'

  interface Props {
    /** Opens the options menu below the button. */
    onoptions: (anchor: DOMRect) => void
  }

  let { onoptions }: Props = $props()

  const maximizeLabel = $derived(host.maximized ? 'Restaurar' : 'Maximizar')
</script>

<!-- data-nodrag keeps a press on a button from starting the window drag (see App.svelte). -->
<div class="controls" data-nodrag>
  <!-- data-menu-anchor: a press here toggles the menu through onclick instead of just dismissing it. -->
  <button
    class="options"
    type="button"
    aria-label="Opciones"
    title="Opciones"
    aria-haspopup="menu"
    data-menu-anchor
    onclick={(e) => onoptions(e.currentTarget.getBoundingClientRect())}
  >
    <svg viewBox="0 0 10 10" aria-hidden="true">
      <path d="M1 2.5h8M1 7.5h8" />
      <circle cx="3.5" cy="2.5" r="1.1" />
      <circle cx="6.5" cy="7.5" r="1.1" />
    </svg>
  </button>
  <button class="minimize" type="button" aria-label="Minimizar" title="Minimizar" onclick={() => send({ type: 'minimize' })}>
    <svg viewBox="0 0 10 10" aria-hidden="true"><path d="M1 5.5h8" /></svg>
  </button>
  <button type="button" aria-label={maximizeLabel} title={maximizeLabel} onclick={() => send({ type: 'toggleMaximize' })}>
    <svg viewBox="0 0 10 10" aria-hidden="true">
      {#if host.maximized}
        <!-- Restore: front window bottom-left, the one behind peeking out top-right. -->
        <path d="M1.5 3.5h5v5h-5z M3.5 3.5v-2h5v5h-2" />
      {:else}
        <path d="M1.5 1.5h7v7h-7z" />
      {/if}
    </svg>
  </button>
  <button class="close" type="button" aria-label="Cerrar" title="Cerrar" onclick={() => send({ type: 'close' })}>
    <svg viewBox="0 0 10 10" aria-hidden="true"><path d="M1 1l8 8M9 1L1 9" /></svg>
  </button>
</div>

<style>
  .controls {
    display: flex;
    margin: -4px -8px -4px auto;
  }
  button {
    display: grid;
    place-items: center;
    width: 30px;
    height: 24px;
    padding: 0;
    border: none;
    border-radius: 4px;
    background: transparent;
    color: var(--text-secondary);
    cursor: default;
    transition: background-color 120ms, color 120ms;
  }
  svg {
    width: 10px;
    height: 10px;
    fill: none;
    stroke: currentColor;
    stroke-width: 1;
    stroke-linecap: round;
    stroke-linejoin: round;
  }
  /* Sliders glyph: the knobs are filled so they read at 10px. */
  .options circle {
    fill: var(--card-background);
  }
  button:hover {
    background: var(--hover);
    color: var(--text);
  }
  .close:hover {
    background: #c42b1c;
    color: #ffffff;
  }
  button:focus-visible {
    outline: 2px solid var(--text-secondary);
    outline-offset: -2px;
  }
  @media (prefers-reduced-motion: reduce) {
    button {
      transition: none;
    }
  }
</style>
