import createClient from 'openapi-fetch'

import type { components, paths } from './schema'

export type Schemas = components['schemas']
export type Dashboard = Schemas['DashboardResponse']
export type DashboardCategory = Schemas['DashboardCategoryResponse']
export type Bookmark = Schemas['BookmarkResponse']
export type DockerStatus = Schemas['DockerStatus']
export type Me = Schemas['MeResponse']
export type Profile = Schemas['ProfileResponse']

/** Names the profile a call acts on; the page sends it from its URL. */
export const PROFILE_HEADER = 'X-Foyer-Profile'

/** Headers for a call within `profile`; none without one, leaving the server to pick. */
export function profileHeaders(profile: string | undefined): Record<string, string> {
  return profile ? { [PROFILE_HEADER]: profile } : {}
}

/**
 * Typed client for Foyer's API, on the page's own origin. fetch is looked up per call so tests
 * can stub it.
 */
export const api = createClient<paths>({
  baseUrl: globalThis.location?.origin ?? '',
  fetch: (request) => globalThis.fetch(request),
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
export type ReorderBookmarksRequest = Schemas['ReorderBookmarksRequest']
export type ImportPreview = Schemas['ImportPreview']
export type ImportPreviewFolder = Schemas['ImportPreviewFolder']
export type ImportResult = Schemas['ImportResult']
