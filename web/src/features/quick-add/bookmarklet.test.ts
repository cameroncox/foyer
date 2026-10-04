import { describe, expect, it, vi } from 'vitest'

import { bookmarkletHref, readQuickAdd } from './bookmarklet.ts'

describe('bookmarkletHref', () => {
  it('opens the quick-add page with the current page’s URL and title', () => {
    const href = bookmarkletHref('http://foyer.lan')
    expect(href.startsWith('javascript:')).toBe(true)

    const open = vi.fn()
    const run = new Function('window', 'location', 'document', href.slice('javascript:'.length))
    run({ open }, { href: 'https://example.com/a?b=c&d' }, { title: 'Example & Co' })

    expect(open).toHaveBeenCalledOnce()
    const [url, name, features] = open.mock.calls[0]
    expect(readQuickAdd(new URL(url).search)).toEqual({
      url: 'https://example.com/a?b=c&d',
      name: 'Example & Co',
      categoryId: undefined,
    })
    expect(new URL(url).pathname).toBe('/add')
    expect(name).toBe('foyer-add')
    expect(features).toContain('popup')
  })

  it('starts the form on the picked category', () => {
    const href = bookmarkletHref('http://foyer.lan', 7)
    const open = vi.fn()
    new Function('window', 'location', 'document', href.slice('javascript:'.length))(
      { open },
      { href: 'https://example.com/' },
      { title: 'Example' },
    )

    expect(readQuickAdd(new URL(open.mock.calls[0][0]).search).categoryId).toBe(7)
  })

  it('has no % for the browser to decode', () => {
    expect(bookmarkletHref('http://foyer.lan:8080')).not.toContain('%')
  })
})

describe('readQuickAdd', () => {
  it('reads blanks when the query string is empty', () => {
    expect(readQuickAdd('')).toEqual({ url: '', name: '', categoryId: undefined })
  })

  it('ignores a category that isn’t an id', () => {
    expect(readQuickAdd('?category=abc').categoryId).toBeUndefined()
    expect(readQuickAdd('?category=0').categoryId).toBeUndefined()
  })
})
