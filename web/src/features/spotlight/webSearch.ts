const SEARCH_URL = 'https://duckduckgo.com/?q='

/** A DuckDuckGo bang ("!g …", "!w …"): the user means the web, not their bookmarks. */
export function isBang(query: string): boolean {
  return /^!\S/.test(query.trim())
}

/** DuckDuckGo results for `query`; it resolves bangs itself. */
export function webSearchUrl(query: string): string {
  return SEARCH_URL + encodeURIComponent(query.trim())
}
