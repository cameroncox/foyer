import { DEFAULT_ACCENT, findAccent } from './accents.ts'

const KEY = 'foyer-accent'

/** The accent saved on this device. Storage can be missing or throw (private windows), so fall back. */
export function loadAccent(): string {
  try {
    return findAccent(localStorage.getItem(KEY)).key
  } catch {
    return DEFAULT_ACCENT
  }
}

export function saveAccent(key: string): void {
  try {
    localStorage.setItem(KEY, key)
  } catch {
    // Not saved; the choice still applies until the page reloads.
  }
}
