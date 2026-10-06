import { describe, expect, it } from 'vitest'

import {
  addressUrl,
  DEFAULT_SEARCH_URL,
  engineName,
  isBang,
  supportsBangs,
  webSearchUrl,
} from './webSearch.ts'

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

describe('addressUrl', () => {
  it('takes a domain to https, keeping a port and path', () => {
    expect(addressUrl('google.com')).toBe('https://google.com')
    expect(addressUrl(' jellyfin.lan/web ')).toBe('https://jellyfin.lan/web')
    expect(addressUrl('foyer.example.org:8443')).toBe('https://foyer.example.org:8443')
  })

  it('keeps an http(s) URL as typed', () => {
    expect(addressUrl('http://router.lan')).toBe('http://router.lan/')
    expect(addressUrl('https://example.com/a?b=c')).toBe('https://example.com/a?b=c')
  })

  it('takes an IPv4 address or localhost to http', () => {
    expect(addressUrl('10.0.10.5:8080')).toBe('http://10.0.10.5:8080')
    expect(addressUrl('localhost:5173')).toBe('http://localhost:5173')
  })

  it('leaves words, versions and spaced queries to the search', () => {
    for (const query of [
      'google',
      'v1.2',
      '3.14',
      'plex 2',
      'c# standard',
      '!g foo',
      'a@b.com',
      '999.1.1.1',
      '',
    ]) {
      expect(addressUrl(query), query).toBeNull()
    }
  })
})
