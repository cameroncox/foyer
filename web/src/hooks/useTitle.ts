import { useEffect } from 'react'

import { useSettings } from '../api/queries.ts'

const DEFAULT_TITLE = 'Foyer'

/**
 * The instance's name (FOYER_TITLE). Empty while it loads, so a custom name doesn't flash
 * "Foyer" first; "Foyer" if the settings can't be read.
 */
export function useTitle() {
  const settings = useSettings()
  return settings.data?.title || (settings.isPending ? '' : DEFAULT_TITLE)
}

/** Keeps the browser tab's title in step with {@link useTitle}. */
export function useDocumentTitle() {
  const title = useTitle()
  useEffect(() => {
    if (title) {
      document.title = title
    }
  }, [title])
}
