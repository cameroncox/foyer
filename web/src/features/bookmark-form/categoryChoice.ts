import type { DashboardCategory } from '../../api/client.ts'

export const NEW_CATEGORY = '__new'

/** Select options: categories in drawer order (Uncategorized last), then "New category…". */
export function categoryOptions(categories: readonly DashboardCategory[]) {
  return [
    ...categories.map((c) => ({ value: String(c.id), label: c.name })),
    { value: NEW_CATEGORY, label: 'New category…' },
  ]
}

/** The request fields for a Category select value plus the new-category name. */
export function categoryFields(value: string, newName: string) {
  return value === NEW_CATEGORY
    ? { categoryId: null, newCategoryName: newName.trim() }
    : { categoryId: Number(value), newCategoryName: null }
}

export function uncategorizedId(categories: readonly DashboardCategory[]): string {
  return String(categories.find((c) => c.isSystem)?.id ?? 1)
}

/** The name of the category a Category select value saves to, for describing where it'll show. */
export function categoryName(
  categories: readonly DashboardCategory[],
  value: string,
  newName: string,
): string {
  if (value === NEW_CATEGORY) {
    return newName.trim() || 'the new category'
  }

  return categories.find((c) => String(c.id) === value)?.name ?? 'Uncategorized'
}
