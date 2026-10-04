/** Opens a bookmark the way clicking its card does. Swapped out in tests. */
export const navigation = {
  open: (url: string) => window.location.assign(url),
}
