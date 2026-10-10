import { beforeEach, describe, expect, it, vi } from 'vitest'

import { api } from './client.ts'

const redirected = () => ({ type: 'opaqueredirect', status: 0, ok: false }) as Response

describe('api', () => {
  let reload: ReturnType<typeof vi.fn>

  beforeEach(() => {
    sessionStorage.clear()
    reload = vi.fn()
    vi.stubGlobal('location', { origin: window.location.origin, reload })
  })

  it('asks fetch not to follow redirects', async () => {
    const fetch = vi.fn(async () => Response.json({}))
    vi.stubGlobal('fetch', fetch)

    await api.GET('/api/settings')

    expect((fetch.mock.calls[0] as unknown as [Request])[0].redirect).toBe('manual')
  })

  it('reloads to sign in when an auth proxy redirects a call', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => redirected()),
    )

    void api.GET('/api/me')

    await vi.waitFor(() => expect(reload).toHaveBeenCalledOnce())
  })

  it('fails instead of reloading again right after a sign-in reload', async () => {
    sessionStorage.setItem('foyer.signInReload', String(Date.now()))
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => redirected()),
    )

    await expect(api.GET('/api/me')).rejects.toThrow('Your sign-in has expired')
    expect(reload).not.toHaveBeenCalled()
  })
})
