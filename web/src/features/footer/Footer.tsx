import { useSettings } from '../../api/queries.ts'
import classes from './Footer.module.css'

export const REPOSITORY_URL = 'https://github.com/cameroncox/foyer'

/** Under the board: the running version and a link to the source. */
export function Footer() {
  const version = useSettings().data?.version

  return (
    <footer className={classes.footer}>
      <span>Foyer{version && ` ${/^\d/.test(version) ? `v${version}` : version}`}</span>
      <span aria-hidden="true">·</span>
      <a href={REPOSITORY_URL} target="_blank" rel="noopener noreferrer">
        GitHub
      </a>
    </footer>
  )
}
