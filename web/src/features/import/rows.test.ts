import { describe, expect, it } from 'vitest'

import type { ImportPreview, ImportPreviewFolder } from '../../api/client.ts'
import { defaultSelection, importRows, selectedCount, totals } from './rows.ts'

const folder = (
  id: string,
  name: string,
  count: number,
  children: ImportPreviewFolder[] = [],
  duplicates = 0,
): ImportPreviewFolder => ({
  id,
  name,
  bookmarkCount: count,
  duplicateCount: duplicates,
  targetCategory: name,
  isNewCategory: true,
  children,
})

const preview: ImportPreview = {
  duplicateCount: 1,
  sections: [
    {
      name: 'Bookmarks bar',
      loose: folder('f1', 'Not in a folder', 1),
      folders: [folder('f2', 'Homelab', 2, [folder('f3', 'Network', 0, [], 1)])],
    },
    { name: 'Other bookmarks', loose: null, folders: [folder('f4', 'Media', 3)] },
  ],
}

describe('import rows', () => {
  it('lists roots as headers and folders by depth, in file order', () => {
    expect(
      importRows(preview).map((r) =>
        r.kind === 'section' ? `# ${r.name}` : `${'  '.repeat(r.depth)}${r.folder.name}`,
      ),
    ).toEqual([
      '# Bookmarks bar',
      'Not in a folder',
      'Homelab',
      '  Network',
      '# Other bookmarks',
      'Media',
    ])
  })

  it('selects folders with something to add', () => {
    expect([...defaultSelection(preview)]).toEqual(['f1', 'f2', 'f4'])
  })

  it('counts the bookmarks the selection would add', () => {
    expect(selectedCount(preview, new Set(['f2', 'f4']))).toBe(5)
  })

  it('totals bookmarks and folders in the file', () => {
    expect(totals(preview)).toEqual({ bookmarks: 7, folders: 4 })
  })
})
