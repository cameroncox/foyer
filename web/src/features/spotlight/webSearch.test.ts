import { describe, expect, it } from 'vitest'

import { DEFAULT_SEARCH_URL, engineName, isBang, supportsBangs, webSearchUrl } from './webSearch.ts'

describe('web search', () => {
  it('spots a bang only at the start, followed by its name', () => {
    expect(isBang('!g current c# standard')).toBe(true)
    expect(isBang('  !w foyer')).toBe(true)
    expect(isBang('! g')).toBe(false)
    expect(isBang('router !g')).toBe(false)
  })

  it('appends the encoded query, or puts it where %s is', () => {
    expect(webSearchUrl(DEFAULT_SEARCH_URL, ' !g current c# standard ')).toBe(
      'https://duckduckgo.com/?q=!g%20current%20c%23%20standard',
    )
    expect(webSearchUrl('https://kagi.com/search?q=%s&r=us', 'a&b')).toBe(
      'https://kagi.com/search?q=a%26b&r=us',
    )
  })

  it('names known engines, and others by host', () => {
    expect(engineName(DEFAULT_SEARCH_URL)).toBe('DuckDuckGo')
    expect(engineName('https://www.google.com/search?q=%s')).toBe('Google')
    expect(engineName('https://searx.lan/search?q=')).toBe('searx.lan')
  })

  it('treats bangs as special only on DuckDuckGo', () => {
    expect(supportsBangs(DEFAULT_SEARCH_URL)).toBe(true)
    expect(supportsBangs('https://www.google.com/search?q=%s')).toBe(false)
  })
})
