import { useMediaQuery } from '@mantine/hooks'

/** Phone width: the same 48em (Mantine's sm) the stylesheets switch layout at. */
export const PHONE_QUERY = '(max-width: 48em)'

/** True below 48em, read on the first render so the layout never flashes the desktop version. */
export function usePhone() {
  return useMediaQuery(PHONE_QUERY, undefined, { getInitialValueInEffect: false })
}
