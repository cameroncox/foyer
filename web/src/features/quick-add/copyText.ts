/**
 * Copies text to the clipboard. The Clipboard API only exists on https and localhost, so on a
 * plain-http Foyer this falls back to selecting a hidden textarea and copying that.
 */
export async function copyText(text: string) {
  if (window.isSecureContext && navigator.clipboard) {
    await navigator.clipboard.writeText(text)
    return
  }

  const area = document.createElement('textarea')
  area.value = text
  area.readOnly = true
  area.style.position = 'fixed'
  area.style.opacity = '0'
  document.body.append(area)
  area.select()
  const copied = document.execCommand('copy')
  area.remove()
  if (!copied) {
    throw new Error('The browser wouldn’t copy it.')
  }
}
