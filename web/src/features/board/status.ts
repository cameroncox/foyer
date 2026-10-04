import type { Bookmark } from '../../api/client.ts'

/** What the status dot means, for its label and the card's tooltip. */
export function statusLabel(bookmark: Bookmark): string | null {
  const docker = bookmark.docker
  if (!docker || !bookmark.status) {
    return null
  }

  if (bookmark.status === 'running') {
    return 'Running'
  }

  if (bookmark.status === 'stopped') {
    return docker.state && docker.state !== 'exited' ? `Stopped (${docker.state})` : 'Stopped'
  }

  if (docker.state === 'running') {
    return docker.health === 'starting' ? 'Starting' : 'Unhealthy'
  }

  return docker.state === 'paused' ? 'Paused' : 'Restarting'
}
