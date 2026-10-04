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

/** The phone's shorter version, a banner above the cards: the top bar has no room for it. */
export function PhoneEditHint({ selecting }: { selecting?: boolean }) {
  return (
    <div className={`${classes.hint} ${classes.phone}`} role="status">
      {selecting
        ? 'Tap cards to pick them. Docker cards can’t be deleted here.'
        : 'Tap a card to edit it. Drag the handle to reorder.'}
    </div>
  )
}
