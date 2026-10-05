/** Opens a bookmark the way clicking its card does: in this tab, in place of Foyer. Swapped out in tests. */
export const navigation = {
  open: (url: string) => {
    window.location.assign(url)
  },
}
