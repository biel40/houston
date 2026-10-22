import { hasHost, send } from './bridge'

export type CardId = 'cpu' | 'gpu'

export const CARD_IDS: readonly CardId[] = ['cpu', 'gpu']

function isCardId(value: string): value is CardId {
  return (CARD_IDS as readonly string[]).includes(value)
}

/** Order and visibility of the hardware cards. The host only stores it; the UI owns its meaning. */
class LayoutState {
  order = $state<CardId[]>([...CARD_IDS])
  hidden = $state<CardId[]>([])
  // Without a host (plain browser) there is nothing to wait for. With one, the cards stay unrendered until
  // the saved layout arrives, so the window never flashes the default layout and resizes twice.
  ready = $state(!hasHost)

  readonly visible = $derived(this.order.filter((id) => !this.hidden.includes(id)))

  /** Applies the saved layout, dropping unknown ids and appending cards added since it was saved. */
  restore(order: readonly string[], hidden: readonly string[]): void {
    const known = [...new Set(order.filter(isCardId))]
    this.order = [...known, ...CARD_IDS.filter((id) => !known.includes(id))]
    this.hidden = hidden.filter(isCardId)
    if (this.visible.length === 0) this.hidden = []
    this.ready = true
  }

  /** Moves a card to a position among the visible ones. Call {@link save} once the gesture ends. */
  move(id: CardId, visibleIndex: number): void {
    const target = this.visible[visibleIndex]
    if (target === undefined || target === id) return
    const next = this.order.filter((other) => other !== id)
    const at = next.indexOf(target)
    next.splice(this.visible.indexOf(id) < visibleIndex ? at + 1 : at, 0, id)
    this.order = next
  }

  setVisible(id: CardId, visible: boolean): void {
    if (visible) {
      this.hidden = this.hidden.filter((other) => other !== id)
    } else if (this.visible.length > 1 && !this.hidden.includes(id)) {
      this.hidden = [...this.hidden, id]
    }
    this.save()
  }

  save(): void {
    send({ type: 'layout', order: [...this.order], hidden: [...this.hidden] })
  }
}

export const layout = new LayoutState()
