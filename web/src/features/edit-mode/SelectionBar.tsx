import { Button, Group, Popover, Text } from '@mantine/core'
import { IconSquareCheck, IconTrash } from '@tabler/icons-react'
import { useState } from 'react'

import classes from './SelectionBar.module.css'

interface Props {
  selecting: boolean
  onSelectingChange: (selecting: boolean) => void
  count: number
  deleting: boolean
  onDelete: () => void
}

/**
 * Edit mode's bar above the cards: Select, or while selecting the count, Cancel and Delete
 * (confirmed first). Sticks under the top bar so Delete stays in reach on a long page.
 */
export function SelectionBar({ selecting, onSelectingChange, count, deleting, onDelete }: Props) {
  const [confirming, setConfirming] = useState(false)
  const noun = count === 1 ? 'bookmark' : 'bookmarks'

  if (!selecting) {
    return (
      <div className={classes.bar}>
        <Button
          variant="default"
          size="xs"
          leftSection={<IconSquareCheck size={14} />}
          onClick={() => onSelectingChange(true)}
        >
          Select
        </Button>
      </div>
    )
  }

  return (
    <div className={classes.bar} data-selecting>
      <Text size="sm" fw={500} className={classes.count} role="status">
        {count === 0 ? 'Pick bookmarks to delete' : `${count} selected`}
      </Text>
      <Button variant="default" size="xs" onClick={() => onSelectingChange(false)}>
        Cancel
      </Button>
      <Popover
        opened={confirming}
        onChange={setConfirming}
        position="bottom-end"
        withArrow
        shadow="md"
      >
        <Popover.Target>
          <Button
            color="red"
            size="xs"
            leftSection={<IconTrash size={14} />}
            disabled={count === 0}
            onClick={() => setConfirming((c) => !c)}
          >
            Delete
          </Button>
        </Popover.Target>
        <Popover.Dropdown>
          <Text size="sm" fw={500}>
            Delete {count} {noun}?
          </Text>
          <Group justify="flex-end" gap="xs" mt="xs">
            <Button variant="default" size="xs" onClick={() => setConfirming(false)}>
              Keep
            </Button>
            <Button
              color="red"
              size="xs"
              loading={deleting}
              onClick={() => {
                setConfirming(false)
                onDelete()
              }}
            >
              Delete {count}
            </Button>
          </Group>
        </Popover.Dropdown>
      </Popover>
    </div>
  )
}
