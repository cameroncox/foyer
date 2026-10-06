const KEY = 'foyer.defaultBannerDismissed'

/** Whether Default's banner was closed on this device. Storage can refuse; then it shows. */
export function isDefaultBannerDismissed(): boolean {
  try {
    return localStorage.getItem(KEY) === 'true'
  } catch {
    return false
  }
}

export function dismissDefaultBanner() {
  try {
    localStorage.setItem(KEY, 'true')
  } catch {
    // Not remembered; it shows again next time.
  }
}
