/** Where the bookmarklet's popup opens; Root shows the quick-add page here instead of the board. */
export const QUICK_ADD_PATH = '/add'

/**
 * The bookmarklet for this Foyer: it opens {@link QUICK_ADD_PATH} in a small popup with the
 * current page's URL and title, and the category the form should start on, if one was picked.
 * A popup rather than a fetch, so the page's CSP, CORS and mixed content rules don't apply,
 * and nothing is saved without the form being submitted here.
 */
export function bookmarkletHref(origin: string, categoryId?: number) {
  const category = categoryId === undefined ? '' : `category=${categoryId}&`
  const target = JSON.stringify(origin + QUICK_ADD_PATH + '?' + category)
  return (
    'javascript:(()=>{window.open(' +
    target +
    '+new URLSearchParams({url:location.href,name:document.title}),' +
    "'foyer-add','popup,width=460,height=720')})()"
  )
}

/** The page the bookmarklet was clicked on, and its category if any, from the query string. */
export function readQuickAdd(search: string) {
  const params = new URLSearchParams(search)
  const categoryId = Number(params.get('category'))
  return {
    url: params.get('url') ?? '',
    name: params.get('name')?.trim() ?? '',
    categoryId: Number.isInteger(categoryId) && categoryId > 0 ? categoryId : undefined,
  }
}
