<script lang="ts">
  interface Props {
    kind: 'cpu' | 'gpu'
  }

  let { kind }: Props = $props()
</script>

<!-- Generic glyphs on the brand color: vendor logos are trademarks, so only their colors are used. -->
<span class="badge" aria-hidden="true">
  <svg viewBox="0 0 16 16">
    {#if kind === 'cpu'}
      <rect x="4" y="4" width="8" height="8" rx="1.5" />
      <rect x="6.5" y="6.5" width="3" height="3" rx="0.5" />
      <path d="M6 1.8v2.2M10 1.8v2.2M6 12v2.2M10 12v2.2M1.8 6h2.2M1.8 10h2.2M12 6h2.2M12 10h2.2" />
    {:else}
      <rect x="1.5" y="4" width="13" height="7.5" rx="1.5" />
      <circle cx="6" cy="7.75" r="2" />
      <path d="M10 6.5h2.5M10 9h2.5M3 11.5v1.8" />
    {/if}
  </svg>
</span>

<style>
  .badge {
    display: flex;
    align-items: center;
    justify-content: center;
    width: 22px;
    height: 22px;
    flex: none;
    border-radius: 6px;
    background: var(--brand);
    color: var(--on-brand);
    transition: background-color 300ms ease-out;
  }
  svg {
    width: 15px;
    height: 15px;
    fill: none;
    stroke: currentColor;
    stroke-width: 1.4;
    stroke-linecap: round;
    stroke-linejoin: round;
  }
  @media (prefers-reduced-motion: reduce) {
    .badge {
      transition: none;
    }
  }
</style>
