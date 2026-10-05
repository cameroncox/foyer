const KEY = 'foyer.profile'

/**
 * The profile last picked on this device, which `/` reopens. Storage can be missing or refuse
 * (private windows, blocked site data), so every access is guarded and failure means no pick.
 */
export function loadRememberedProfile(): string | undefined {
  try {
    return localStorage.getItem(KEY) ?? undefined
  } catch {
    return undefined
  }
}

export function rememberProfile(slug: string) {
  try {
    localStorage.setItem(KEY, slug)
  } catch {
    // Not remembered; `/` opens the default profile instead.
  }
}

export function forgetProfile() {
  try {
    localStorage.removeItem(KEY)
  } catch {
    // Nothing to forget.
  }
}
