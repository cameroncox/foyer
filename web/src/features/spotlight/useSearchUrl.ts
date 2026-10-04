import { useSettings } from '../../api/queries.ts'
import { DEFAULT_SEARCH_URL } from './webSearch.ts'

/** The spotlight's web search template (FOYER_SEARCH_URL); DuckDuckGo until settings load. */
export function useSearchUrl() {
  return useSettings().data?.searchUrl || DEFAULT_SEARCH_URL
}
