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
