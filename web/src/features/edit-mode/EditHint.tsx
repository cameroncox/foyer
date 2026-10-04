import { IconPencil } from '@tabler/icons-react'

import classes from './EditHint.module.css'

/** The edit-mode explanation, shown in the top bar in place of search. */
export function EditHint({ className }: { className?: string }) {
  return (
    <div className={`${classes.hint} ${className ?? ''}`} role="status">
      <IconPencil size={16} aria-hidden="true" style={{ flex: 'none' }} />
      <span>
        Editing — drag cards by their handle, into other categories too. Docker cards keep their
        labels&apos; name, URL and icon. Changes save as you go.
      </span>
    </div>
  )
}
