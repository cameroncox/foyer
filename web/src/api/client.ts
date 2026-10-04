import createClient from 'openapi-fetch'

import type { components, paths } from './schema'

export type Schemas = components['schemas']
export type Dashboard = Schemas['DashboardResponse']
export type DashboardCategory = Schemas['DashboardCategoryResponse']
export type Bookmark = Schemas['BookmarkResponse']
export type DockerStatus = Schemas['DockerStatus']

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
