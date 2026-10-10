import createClient from 'openapi-fetch'

import type { components, paths } from './schema'

export type Schemas = components['schemas']
export type Dashboard = Schemas['DashboardResponse']
export type DashboardCategory = Schemas['DashboardCategoryResponse']
export type Bookmark = Schemas['BookmarkResponse']
export type DockerStatus = Schemas['DockerStatus']
export type Me = Schemas['MeResponse']
export type Profile = Schemas['ProfileResponse']
export type ShareTarget = Schemas['ShareTargetResponse']

/** Names the profile a call acts on; the page sends it from its URL. */
export const PROFILE_HEADER = 'X-Foyer-Profile'

/** Headers for a call within `profile`; none without one, leaving the server to pick. */
export function profileHeaders(profile: string | undefined): Record<string, string> {
  return profile ? { [PROFILE_HEADER]: profile } : {}
}

const SIGN_IN_RELOAD_KEY = 'foyer.signInReload'

/**
 * The API never redirects, so a redirect means an auth proxy wants a sign-in. fetch can't follow
 * it to the proxy's login page, but a reload can. A second redirect within a minute of that
 * reload is an error instead, so a misbehaving proxy can't loop the page.
 */
function signInAgain(): Promise<never> {
  let last = 0
  try {
    last = Number(sessionStorage.getItem(SIGN_IN_RELOAD_KEY)) || 0
  } catch {
    // Without storage there's no loop guard; reload anyway.
  }

  if (Date.now() - last < 60_000) {
    return Promise.reject(new Error('Your sign-in has expired. Reload the page to sign in again.'))
  }

  try {
    sessionStorage.setItem(SIGN_IN_RELOAD_KEY, String(Date.now()))
  } catch {
    // As above.
  }

  globalThis.location.reload()
  // The page is going away; never settle, so nothing flashes an error first.
  return new Promise(() => {})
}

/**
 * Typed client for Foyer's API, on the page's own origin. fetch is looked up per call so tests
 * can stub it.
 */
export const api = createClient<paths>({
  baseUrl: globalThis.location?.origin ?? '',
  fetch: async (request) => {
    const response = await globalThis.fetch(new Request(request, { redirect: 'manual' }))
    return response.type === 'opaqueredirect' ? signInAgain() : response
  },
})

/** Thrown for any non-2xx answer, carrying the problem details' text when there is one. */
export class ApiError extends Error {
  readonly status: number

  constructor(status: number, detail?: string | null) {
    super(detail || `Request failed (${status})`)
    this.status = status
  }
}

/** Unwraps an openapi-fetch result, throwing ApiError on failure. */
export function unwrap<T>(result: { data?: T; error?: unknown; response: Response }): T {
  if (result.error !== undefined || !result.response.ok) {
    const detail = (result.error as { detail?: string } | undefined)?.detail
    throw new ApiError(result.response.status, detail)
  }

  return result.data as T
}

export type CreateBookmarkRequest = Schemas['CreateBookmarkRequest']
export type UpdateBookmarkRequest = Schemas['UpdateBookmarkRequest']
export type UpdateProfileRequest = Schemas['UpdateProfileRequest']
export type ReorderBookmarksRequest = Schemas['ReorderBookmarksRequest']
export type ImportPreview = Schemas['ImportPreview']
export type ImportPreviewFolder = Schemas['ImportPreviewFolder']
export type ImportResult = Schemas['ImportResult']
