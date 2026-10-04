import classes from './EditBanner.module.css'

export function EditBanner() {
  return (
    <div className={classes.banner} role="status">
      Editing — drag cards by their handle to reorder them, or drop them into another category.
      Docker cards take their name, URL and icon from labels; their category and tags can be changed
      here. Changes save as you go.
    </div>
  )
}
