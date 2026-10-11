/** Letters, digits and hyphens, as the server checks. */
const NAME = /^[A-Za-z0-9-]+$/

export function validateName(value: string) {
  const name = value.trim()
  if (!name) {
    return 'Give it a name.'
  }

  return NAME.test(name) ? null : 'Use letters, digits and hyphens only.'
}
