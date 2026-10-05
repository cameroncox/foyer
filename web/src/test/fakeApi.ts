import { vi } from 'vitest'

import type { Dashboard } from '../api/client.ts'
import { profilesOff } from './fakes.ts'

export interface ApiCall {
  method: string
  path: string
  body: unknown
  /** The X-Foyer-Profile header, when sent. */
  profile: string | null
}

type Handler = (call: ApiCall) => unknown | Response

const json = (value: unknown, status = 200) =>
  new Response(value === undefined ? null : JSON.stringify(value), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })

const READS = new Set(['GET /api/dashboard', 'GET /api/settings', 'GET /api/me'])

/**
 * Stubs fetch with a tiny router: GET /api/dashboard serves `dashboard()`, GET /api/settings
 * the default title, GET /api/me profiles off, other calls hit `routes` by "METHOD /path"
 * (default: 204), and every call except those reads is recorded with its JSON body.
 */
export function stubApi(dashboard: () => Dashboard, routes: Record<string, Handler> = {}) {
  const calls: ApiCall[] = []
  vi.stubGlobal(
    'fetch',
    vi.fn(async (request: Request) => {
      const path = new URL(request.url).pathname
      const text = request.method === 'GET' ? '' : await request.text()
      const call = {
        method: request.method,
        path,
        body: text ? JSON.parse(text) : undefined,
        profile: request.headers.get('X-Foyer-Profile'),
      }
      const route = `${request.method} ${path}`
      if (!READS.has(route)) {
        calls.push(call)
      }

      const handler = routes[route]
      if (handler) {
        const result = handler(call)
        return result instanceof Response ? result : json(result)
      }

      if (route === 'GET /api/dashboard') {
        return json(dashboard())
      }

      if (route === 'GET /api/me') {
        return json(profilesOff())
      }

      return route === 'GET /api/settings'
        ? json({ title: 'Foyer', searchUrl: 'https://duckduckgo.com/?q=', version: '1.0.3' })
        : new Response(null, { status: 204 })
    }),
  )

  return {
    calls,
    /** The recorded calls of one kind, e.g. 'POST /api/bookmarks'. */
    called: (route: string) => calls.filter((c) => `${c.method} ${c.path}` === route),
  }
}

export const problem = (status: number, detail: string) =>
  new Response(JSON.stringify({ status, detail }), {
    status,
    headers: { 'Content-Type': 'application/problem+json' },
  })
