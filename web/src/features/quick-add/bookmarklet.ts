/** Where the bookmarklet's popup opens; Root shows the quick-add page here instead of the board. */
export const QUICK_ADD_PATH = '/add'

/**
 * The bookmarklet for this Foyer: it opens {@link QUICK_ADD_PATH} in a small popup with the
 * current page's URL and title. A popup rather than a fetch, so the page's CSP, CORS and mixed
 * content rules don't apply, and nothing is saved without the form being submitted here.
 */
export function bookmarkletHref(origin: string) {
  const target = JSON.stringify(origin + QUICK_ADD_PATH + '?')
  return (
    'javascript:(()=>{window.open(' +
    target +
    '+new URLSearchParams({url:location.href,name:document.title}),' +
    "'foyer-add','popup,width=460,height=720')})()"
  )
}

/** The page the bookmarklet was clicked on, from the quick-add page's query string. */
export function readQuickAdd(search: string) {
  const params = new URLSearchParams(search)
  return { url: params.get('url') ?? '', name: params.get('name')?.trim() ?? '' }
}
