import { ActionIcon, Button, Kbd, Tooltip, UnstyledButton } from '@mantine/core'
import { IconCheck, IconList, IconMenu2, IconPencil, IconSearch } from '@tabler/icons-react'
import { useState } from 'react'

import { usePhone } from '../../hooks/usePhone.ts'
import { useTitle } from '../../hooks/useTitle.ts'
import { EditHint } from '../edit-mode/EditHint.tsx'
import { useCanEdit } from '../profiles/profileContext.ts'
import { type ProfileDialog, ProfileDialogs } from '../profiles/ProfileDialogs.tsx'
import { ProfilePicker } from '../profiles/ProfilePicker.tsx'
import { PhoneMenu } from './PhoneMenu.tsx'
import { ThemeMenu } from './ThemeMenu.tsx'
import classes from './TopBar.module.css'

interface Props {
  editing: boolean
  onEditingChange: (editing: boolean) => void
  /** Phone menu's Add bookmark. */
  onAdd: () => void
  /** The search button: opens the spotlight. */
  onSearch: () => void
  /** Phone edit mode's Categories button; the drawer is a full screen there. */
  onOpenCategories: () => void
}

interface BarProps extends Props {
  onDialog: (dialog: ProfileDialog) => void
}

/**
 * The Foyer name and profile picker, a centered button dressed as a search box that opens the
 * spotlight, the theme picker and an Edit icon (Done while editing). On a phone: the name, a
 * search icon, and a menu holding the rest, or Categories and Done while editing. Edit isn't
 * offered on a profile the page can't change.
 */
export function TopBar(props: Props) {
  const phone = usePhone()
  const [dialog, setDialog] = useState<ProfileDialog>(null)

  return (
    <>
      {phone ? (
        <PhoneTopBar {...props} onDialog={setDialog} />
      ) : (
        <DesktopTopBar {...props} onDialog={setDialog} />
      )}
      <ProfileDialogs dialog={dialog} onDialog={setDialog} />
    </>
  )
}

function DesktopTopBar({ editing, onEditingChange, onSearch, onDialog }: BarProps) {
  const title = useTitle()
  const canEdit = useCanEdit()

  return (
    <div className={classes.bar}>
      <div className={classes.inner}>
        <div className={classes.brand}>
          <span className={classes.title}>{title}</span>
          {!editing && <ProfilePicker onDialog={onDialog} />}
        </div>
        {editing ? (
          <EditHint className={classes.search} />
        ) : (
          <UnstyledButton
            className={`${classes.search} ${classes.searchButton}`}
            aria-label="Search bookmarks"
            onClick={onSearch}
          >
            <IconSearch size={18} stroke={2} aria-hidden="true" />
            <span className={classes.searchLabel}>Search bookmarks</span>
            <Kbd size="xs" aria-hidden="true">
              Space
            </Kbd>
          </UnstyledButton>
        )}
        <div className={classes.actions}>
          <ThemeMenu />
          {editing ? (
            <Button
              size="md"
              leftSection={<IconCheck size={16} />}
              onClick={() => onEditingChange(false)}
            >
              Done
            </Button>
          ) : (
            canEdit && (
              <Tooltip label="Edit page" withinPortal>
                <ActionIcon
                  variant="default"
                  size={44}
                  radius="md"
                  aria-label="Edit page"
                  onClick={() => onEditingChange(true)}
                >
                  <IconPencil size={20} stroke={1.8} />
                </ActionIcon>
              </Tooltip>
            )
          )}
        </div>
      </div>
    </div>
  )
}

function PhoneTopBar({
  editing,
  onEditingChange,
  onAdd,
  onSearch,
  onOpenCategories,
  onDialog,
}: BarProps) {
  const [menuOpen, setMenuOpen] = useState(false)
  const title = useTitle()

  if (editing) {
    return (
      <div className={classes.bar}>
        <div className={classes.inner}>
          <div className={classes.editingTitle}>Editing</div>
          <Button variant="default" leftSection={<IconList size={16} />} onClick={onOpenCategories}>
            Categories
          </Button>
          <Button onClick={() => onEditingChange(false)}>Done</Button>
        </div>
      </div>
    )
  }

  return (
    <div className={classes.bar}>
      <div className={classes.inner}>
        <div className={classes.brand}>
          <span className={classes.title}>{title}</span>
        </div>
        <ActionIcon
          variant="subtle"
          color="gray"
          size={44}
          radius="md"
          aria-label="Search bookmarks"
          onClick={onSearch}
        >
          <IconSearch size={22} />
        </ActionIcon>
        <ActionIcon
          variant="subtle"
          color="gray"
          size={44}
          radius="md"
          aria-label="Open menu"
          aria-expanded={menuOpen}
          onClick={() => setMenuOpen(true)}
        >
          <IconMenu2 size={22} />
        </ActionIcon>
      </div>
      <PhoneMenu
        title={title}
        opened={menuOpen}
        onClose={() => setMenuOpen(false)}
        onAdd={onAdd}
        onEdit={() => onEditingChange(true)}
        onDialog={onDialog}
      />
    </div>
  )
}
