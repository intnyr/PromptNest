# PromptNest Visual QA Checklist

Reference: selected dark desktop PromptNest mockup.

## Viewports

- Desktop: verify 1366x768 or wider keeps rail, sidebar, prompt list, and editor visible without overlap.
- Narrow: verify widths below 1040 collapse the editor pane instead of clipping list/sidebar content.
- High contrast: verify text remains readable and focus rings remain visible.
- Reduced motion: verify no required workflow depends on animation.

## Keyboard

- Ctrl+K and Ctrl+F focus search.
- Ctrl+N triggers New prompt command placeholder.
- Ctrl+D triggers Duplicate command placeholder.
- Ctrl+E focuses the prompt body editor.
- F2 focuses the title field.
- Delete routes to the current destructive/overflow command placeholder.
- Ctrl+, opens Settings.
- Win+Shift+Space toggles the palette when the hotkey is available.
- Palette supports Up, Down, Enter, and Escape.

## Main Window

- Title bar, rail, sidebar, prompt list, and editor align with the selected reference.
- Prompt rows keep title, date, favorite, preview, and tags readable at supported widths.
- Search, toolbar, sort, pagination, and selection states do not shift layout.
- Settings panel opens from the rail and does not cover navigation controls.
- Disabled import/export/backup controls clearly show unavailable state and tooltip context.

## Accessibility

- Icon-only controls have automation names or tooltips.
- Core controls are reachable by keyboard tab order.
- Focus visuals are visible on toolbar buttons, prompt rows, tag controls, editor fields, settings controls, and palette results.
- Validation success/failure text uses both wording and color.

## Palette

- Palette uses dark surface, search field, result rows, and footer hints consistent with the main app.
- Result list stays within 600x420 window and shows up to 8 items.
- Copy status is announced in the palette footer text.
