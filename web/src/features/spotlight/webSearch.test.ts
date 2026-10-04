import { describe, expect, it } from 'vitest'

import { isBang, webSearchUrl } from './webSearch.ts'

describe('web search', () => {
  it('spots a bang only at the start, followed by its name', () => {
    expect(isBang('!g current c# standard')).toBe(true)
    expect(isBang('  !w foyer')).toBe(true)
    expect(isBang('! g')).toBe(false)
    expect(isBang('router !g')).toBe(false)
  })

  it('builds a DuckDuckGo URL with the query encoded', () => {
    expect(webSearchUrl(' !g current c# standard ')).toBe(
      'https://duckduckgo.com/?q=!g%20current%20c%23%20standard',
    )
  })
})
