import {
  Alert,
  Badge,
  Button,
  Checkbox,
  FileInput,
  Group,
  Modal,
  ScrollArea,
  Stack,
  Text,
} from '@mantine/core'
import { notifications } from '@mantine/notifications'
import { IconFileUpload } from '@tabler/icons-react'
import { useState } from 'react'

import type { ImportPreview } from '../../api/client.ts'
import { useImport, usePreviewImport } from '../../api/mutations.ts'
import { usePhone } from '../../hooks/usePhone.ts'
import classes from './ImportModal.module.css'
import { defaultSelection, importRows, selectedCount, totals } from './rows.ts'

interface Props {
  opened: boolean
  onClose: () => void
}

/**
 * Import a browser bookmark export: pick the file, review the folder tree (each folder becomes a
 * category, duplicates are skipped), pick folders, import. Nothing is saved before Import.
 */
export function ImportModal({ opened, onClose }: Props) {
  const preview = usePreviewImport()
  const importer = useImport()
  const phone = usePhone()
  const [file, setFile] = useState<{ name: string; html: string } | null>(null)
  const [selected, setSelected] = useState<Set<string>>(new Set())

  const reset = () => {
    preview.reset()
    importer.reset()
    setFile(null)
    setSelected(new Set())
  }

  const close = () => {
    reset()
    onClose()
  }

  const pick = async (picked: File | null) => {
    reset()
    if (!picked) {
      return
    }

    const html = await picked.text()
    setFile({ name: picked.name, html })
    preview.mutate(html, { onSuccess: (result) => setSelected(defaultSelection(result)) })
  }

  const toggle = (id: string) =>
    setSelected((current) => {
      const next = new Set(current)
      if (!next.delete(id)) {
        next.add(id)
      }

      return next
    })

  const run = () => {
    if (!file) {
      return
    }

    importer.mutate(
      { html: file.html, folderIds: [...selected] },
      {
        onSuccess: (result) => {
          notifications.show({
            color: 'green',
            title: `Imported ${result.added} ${result.added === 1 ? 'bookmark' : 'bookmarks'}`,
            message:
              result.skipped > 0 ? `${result.skipped} already in Foyer were skipped.` : undefined,
          })
          close()
        },
      },
    )
  }

  const data = preview.data
  const count = data ? selectedCount(data, selected) : 0

  return (
    <Modal
      opened={opened}
      onClose={close}
      title="Import bookmarks"
      size="xl"
      fullScreen={phone}
      scrollAreaComponent={ScrollArea.Autosize}
    >
      <Stack gap="md">
        <FileInput
          label="Bookmark file"
          description="Export from Chrome, Firefox, Edge or Safari (an .html file)."
          placeholder="Choose file"
          accept=".html,.htm,text/html"
          leftSection={<IconFileUpload size={16} />}
          value={null}
          onChange={pick}
          clearable={false}
        />
        {preview.error && <Alert color="red">{preview.error.message}</Alert>}
        {importer.error && <Alert color="red">{importer.error.message}</Alert>}
        {preview.isPending && <Text c="dimmed">Reading {file?.name}…</Text>}
        {data && file && (
          <PreviewTable file={file.name} preview={data} selected={selected} onToggle={toggle} />
        )}

        <Group justify="flex-end" gap="xs">
          {data && data.duplicateCount > 0 && (
            <Text size="sm" c="dimmed" mr="auto">
              {data.duplicateCount} {data.duplicateCount === 1 ? 'bookmark' : 'bookmarks'} already
              in Foyer will be skipped.
            </Text>
          )}
          <Button variant="default" onClick={close}>
            Cancel
          </Button>
          <Button onClick={run} disabled={!data || count === 0} loading={importer.isPending}>
            Import {count} {count === 1 ? 'bookmark' : 'bookmarks'}
          </Button>
        </Group>
      </Stack>
    </Modal>
  )
}

interface TableProps {
  file: string
  preview: ImportPreview
  selected: ReadonlySet<string>
  onToggle: (id: string) => void
}

function PreviewTable({ file, preview, selected, onToggle }: TableProps) {
  const { bookmarks, folders } = totals(preview)
  const rows = importRows(preview)

  if (rows.length === 0) {
    return (
      <Alert color="yellow">No bookmarks found in {file}. Is it a browser bookmark export?</Alert>
    )
  }

  return (
    <Stack gap={8}>
      <Text size="sm" c="dimmed">
        <Text span ff="monospace" size="sm">
          {file}
        </Text>{' '}
        · {bookmarks} bookmarks in {folders} folders
      </Text>
      <div className={classes.table} role="table" aria-label="Folders to import">
        <div className={classes.head} role="row">
          <span role="columnheader">Folder</span>
          <span role="columnheader" style={{ textAlign: 'right' }}>
            Bookmarks
          </span>
          <span role="columnheader">Goes to</span>
        </div>
        {rows.map((row) =>
          row.kind === 'section' ? (
            <div key={row.key} className={classes.section} role="row">
              <span role="rowheader">{row.name}</span>
            </div>
          ) : (
            <div key={row.key} className={classes.row} role="row">
              <div role="cell" style={{ paddingLeft: row.depth * 20 }}>
                <Checkbox
                  label={row.folder.name}
                  checked={selected.has(row.folder.id)}
                  disabled={row.folder.bookmarkCount === 0}
                  onChange={() => onToggle(row.folder.id)}
                />
              </div>
              <span className={classes.count} role="cell">
                {row.folder.bookmarkCount}
              </span>
              <span className={classes.dest} role="cell">
                <span aria-hidden="true">→</span>
                <span>{row.folder.targetCategory}</span>
                {row.folder.isNewCategory && (
                  <Badge size="xs" variant="light">
                    new
                  </Badge>
                )}
              </span>
            </div>
          ),
        )}
      </div>
    </Stack>
  )
}
