import { fireEvent, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'

import { bookmark } from '../test/fakes.ts'
import { render } from '../test/render.tsx'
import { BookmarkIcon } from './BookmarkIcon.tsx'

describe('BookmarkIcon', () => {
  it('shows the first letter when there is no icon', () => {
    render(<BookmarkIcon bookmark={bookmark('proxmox')} />)
    expect(screen.getByText('P')).toBeInTheDocument()
  })

  it('falls back to the letter when the icon fails to load', () => {
    const { container } = render(
      <BookmarkIcon bookmark={bookmark('Gitea', { iconUrl: '/api/icons/abc' })} />,
    )
    fireEvent.error(container.querySelector('img')!)
    expect(screen.getByText('G')).toBeInTheDocument()
    expect(container.querySelector('img')).toBeNull()
  })

  it('marks uncolored mdi/si icons as monochrome for dark mode', () => {
    const { container, rerender } = render(
      <BookmarkIcon bookmark={bookmark('a', { icon: 'mdi-home', iconUrl: '/api/icons/a' })} />,
    )
    expect(container.querySelector('img')!.className).toMatch(/monochrome/)

    rerender(
      <BookmarkIcon
        bookmark={bookmark('b', { icon: 'mdi-home-#f0d453', iconUrl: '/api/icons/b' })}
      />,
    )
    expect(container.querySelector('img')!.className).not.toMatch(/monochrome/)
  })
})
