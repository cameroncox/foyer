/** Opens a bookmark the way clicking its card does: in a new tab. Swapped out in tests. */
export const navigation = {
  open: (url: string) => {
    window.open(url, '_blank', 'noopener,noreferrer')
  },
}
