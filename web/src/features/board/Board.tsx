import type { DashboardCategory } from '../../api/client.ts'
import classes from './Board.module.css'
import { BookmarkCard } from './BookmarkCard.tsx'

/** Categories in drawer order, each a grid of cards. Categories with nothing to show are hidden. */
export function Board({ categories }: { categories: readonly DashboardCategory[] }) {
  return (
    <div className={classes.board}>
      {categories
        .filter((c) => c.bookmarks.length > 0)
        .map((category) => (
          <section
            key={category.id}
            className={classes.section}
            aria-labelledby={`category-${category.id}`}
          >
            <h2 id={`category-${category.id}`} className={classes.heading}>
              {category.name}
              <span className={classes.count}>{category.bookmarks.length}</span>
            </h2>
            <div className={classes.grid}>
              {category.bookmarks.map((bookmark) => (
                <BookmarkCard key={bookmark.id} bookmark={bookmark} />
              ))}
            </div>
          </section>
        ))}
    </div>
  )
}
