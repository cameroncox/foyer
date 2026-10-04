import type { ImportPreview, ImportPreviewFolder } from '../../api/client.ts'

export type ImportRow =
  | { kind: 'section'; key: string; name: string }
  | { kind: 'folder'; key: string; folder: ImportPreviewFolder; depth: number }

/** The preview tree as table rows: each root as a header, then its folders indented by depth. */
export function importRows(preview: ImportPreview): ImportRow[] {
  const rows: ImportRow[] = []
  const walk = (folder: ImportPreviewFolder, depth: number) => {
    rows.push({ kind: 'folder', key: folder.id, folder, depth })
    folder.children.forEach((child) => walk(child, depth + 1))
  }

  preview.sections.forEach((section, i) => {
    rows.push({ kind: 'section', key: `section-${i}`, name: section.name })
    if (section.loose) {
      walk(section.loose, 0)
    }

    section.folders.forEach((folder) => walk(folder, 0))
  })
  return rows
}

/** Folders with something to add, which start selected. */
export function defaultSelection(preview: ImportPreview): Set<string> {
  return new Set(
    importRows(preview).flatMap((r) =>
      r.kind === 'folder' && r.folder.bookmarkCount > 0 ? [r.folder.id] : [],
    ),
  )
}

export function selectedCount(preview: ImportPreview, selected: ReadonlySet<string>): number {
  return importRows(preview).reduce(
    (sum, r) =>
      sum + (r.kind === 'folder' && selected.has(r.folder.id) ? r.folder.bookmarkCount : 0),
    0,
  )
}

export function totals(preview: ImportPreview): { bookmarks: number; folders: number } {
  const folders = importRows(preview).flatMap((r) => (r.kind === 'folder' ? [r.folder] : []))
  return {
    bookmarks: folders.reduce((sum, f) => sum + f.bookmarkCount + f.duplicateCount, 0),
    folders: folders.length,
  }
}
