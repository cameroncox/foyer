import { vi } from 'vitest'

import type { Dashboard } from '../api/client.ts'

export interface ApiCall {
  method: string
  path: string
  body: unknown
}

type Handler = (call: ApiCall) => unknown | Response

const json = (value: unknown, status = 200) =>
  new Response(value === undefined ? null : JSON.stringify(value), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })

/**
 * Stubs fetch with a tiny router: GET /api/dashboard serves `dashboard()`, GET /api/settings
 * the default title, other calls hit `routes` by "METHOD /path" (default: 204), and every call
 * except those two reads is recorded with its JSON body.
 */
export function stubApi(dashboard: () => Dashboard, routes: Record<string, Handler> = {}) {
  const calls: ApiCall[] = []
  vi.stubGlobal(
    'fetch',
    vi.fn(async (request: Request) => {
      const path = new URL(request.url).pathname
      const text = request.method === 'GET' ? '' : await request.text()
      const call = { method: request.method, path, body: text ? JSON.parse(text) : undefined }
      const route = `${request.method} ${path}`
      if (route !== 'GET /api/dashboard' && route !== 'GET /api/settings') {
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

      return route === 'GET /api/settings'
        ? json({ title: 'Foyer', searchUrl: 'https://duckduckgo.com/?q=' })
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
