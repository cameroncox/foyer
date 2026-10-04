import { screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'

import { bookmark, dockerBookmark } from '../../test/fakes.ts'
import { render } from '../../test/render.tsx'
import { BookmarkCard } from './BookmarkCard.tsx'

describe('BookmarkCard', () => {
  it('shows every tag when there are two or fewer', () => {
    render(<BookmarkCard bookmark={bookmark('Gitea', { tags: ['dev', 'git'] })} />)
    expect(screen.getByText('#dev')).toBeInTheDocument()
    expect(screen.getByText('#git')).toBeInTheDocument()
    expect(screen.queryByText(/^\+\d+$/)).toBeNull()
  })

  it('shows two tags and collapses the rest into a count', () => {
    const days = ['mon', 'tue', 'wed', 'thur', 'fri', 'sat', 'sun']
    render(<BookmarkCard bookmark={bookmark('Comic', { tags: days })} />)
    expect(screen.getByText('#mon')).toBeInTheDocument()
    expect(screen.getByText('#tue')).toBeInTheDocument()
    expect(screen.queryByText('#wed')).toBeNull()
    expect(screen.getByText('+5')).toBeInTheDocument()
  })

  it('counts the host tag as one of the two', () => {
    render(
      <BookmarkCard bookmark={dockerBookmark('Sonarr', 'running', { tags: ['media', 'tv'] })} />,
    )
    expect(screen.getByText('#docker-4')).toBeInTheDocument()
    expect(screen.getByText('#media')).toBeInTheDocument()
    expect(screen.queryByText('#tv')).toBeNull()
    expect(screen.getByText('+1')).toBeInTheDocument()
  })
})
