/** The spotlight's web search when FOYER_SEARCH_URL isn't set, or settings haven't loaded. */
export const DEFAULT_SEARCH_URL = 'https://duckduckgo.com/?q='

const ENGINE_NAMES: Record<string, string> = {
  'duckduckgo.com': 'DuckDuckGo',
  'google.com': 'Google',
  'bing.com': 'Bing',
  'kagi.com': 'Kagi',
  'search.brave.com': 'Brave Search',
  'startpage.com': 'Startpage',
}

function hostOf(template: string): string {
  try {
    return new URL(template.replaceAll('%s', '')).hostname.replace(/^www\./, '')
  } catch {
    return template
  }
}

/** The engine's name for the spotlight row: a known one by name, anything else by its host. */
export function engineName(template: string): string {
  const host = hostOf(template)
  return ENGINE_NAMES[host] ?? host
}

/** Bangs ("!g …") are DuckDuckGo's; other engines just search the text. */
export function supportsBangs(template: string): boolean {
  return engineName(template) === 'DuckDuckGo'
}

/** A bang query: the user means the web, not their bookmarks. */
export function isBang(query: string): boolean {
  return /^!\S/.test(query.trim())
}

/** The search URL for `query`: it replaces %s in the template, or is appended when there's none. */
export function webSearchUrl(template: string, query: string): string {
  const encoded = encodeURIComponent(query.trim())
  return template.includes('%s') ? template.replaceAll('%s', encoded) : template + encoded
}

const IPV4 = /^(\d{1,3})\.(\d{1,3})\.(\d{1,3})\.(\d{1,3})$/
// Letters for the last label, so "v1.2" or "3.14" isn't taken for a site.
const DOMAIN = /^(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+[a-z]{2,}$/i

/**
 * Where the query goes as an address, or null when it isn't one: an http(s) URL as typed, a
 * domain ("google.com", "jellyfin.lan/web") over https, an IPv4 address or localhost over
 * http, which is what LAN services usually speak. A port and path may follow.
 */
export function addressUrl(query: string): string | null {
  const text = query.trim()
  if (text === '' || /\s/.test(text)) {
    return null
  }

  const explicit = /^https?:\/\//i.test(text)
  let url: URL
  try {
    url = new URL(explicit ? text : `http://${text}`)
  } catch {
    return null
  }

  if (explicit) {
    return url.href
  }

  // The URL parser takes much that isn't an address ("3.14" as 3.0.0.14, a "user@" prefix), so
  // the part before any port or path must be the hostname as typed.
  const host = url.hostname
  if (text.split(/[:/?#]/, 1)[0].toLowerCase() !== host) {
    return null
  }

  const ipv4 = IPV4.exec(host)
  if (host === 'localhost' || ipv4?.slice(1).every((n) => Number(n) <= 255)) {
    return `http://${text}`
  }

  return DOMAIN.test(host) ? `https://${text}` : null
}
