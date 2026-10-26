# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Nx** is a Blazor component library (.NET 10) that will replace Telerik in the ERP portal. Components so far:
`NxButton`, `NxTextBox`, `NxNumericTextBox<TValue>`, `NxComboBox<TItem, TValue>`, `NxCheckBox<TValue>`,
`NxToggleSwitch`, `NxRadioGroup<TValue>` (+ `NxRadioButton`), `NxToggleGroup<TValue>` (+ `NxToggleGroupItem`),
`NxDatePicker<TValue>`, `NxDateRangePicker<TValue>`, `NxTimePicker<TValue>`, `NxCalendar`, `NxWindow`, `NxTabStrip` (+ `NxTab`),
`NxSplitter` (+ `NxSplitterPane`), `NxDropDown<TItem, TValue>`, `NxMultiSelect<TItem, TValue>`, `NxTextArea`, `NxTooltip`,
`NxMenu`, `NxContextMenu`, `NxDropDownButton`, `NxSplitButton`, `NxAutoComplete<TItem>`, `NxDateTimePicker<TValue>`,
`NxForm` (+ `NxFormItem`, `NxFormGroup`, `NxValidationMessage`), `NxIcon` (+ `NxIcons`), `NxLoader`, `NxLoaderContainer`,
`NxButtonGroup`, `NxColorPicker`, `NxProgressBar`, `NxNotification`, `NxDialog` (+ `NxDialogService`, `NxDialogHost`), `NxMediaQuery`,
`NxToggleButton`, `NxUpload`, `NxFileSelect`, `NxTreeView<TItem>`, `NxMaskedTextBox`, `NxTileLayout` (+ `NxTileLayoutItem`),
`NxCarousel<TItem>`, `NxColorGradient`, `NxSlider<TValue>`, `NxChipList<TItem>`, `NxToolBar` (+ `NxToolBarButton`, `NxToolBarSeparator`,
`NxToolBarSpacer`), `NxDateInput<TValue>`, `NxChart` (+ `NxChartSeries`, `NxChartCategoryAxis`, `NxChartValueAxis`). No third-party dependencies; themed by CSS custom properties.
Conventions follow the NxGrid repo (`../nxgrid`), which stays where it is.

## Build and Test Commands

```bash
dotnet build Nx.slnx                         # must stay at zero warnings
dotnet test tests/Nx.Components.Tests        # bUnit, no app needed
dotnet test tests/Nx.Playwright              # needs the demo running on :44420 (or NX_DEMO_URL)
dotnet run --project samples/Nx.Demo         # binds http://0.0.0.0:44420
```

In a Treeline worktree, start the demo with `start_watch`. Hot reload often fails on code-behind signature
changes (500 with `TypeLoadException`); restart the watch rather than chasing it.

## Project Structure

```
src/Nx.Components/
  NxButton.razor / .razor.cs / .razor.css   # markup, parameters + logic, scoped layout CSS
  NxTextBox.razor / .razor.cs / .razor.css
  NxComboBox.razor / .razor.cs + partials    # .Filtering .Selection .Multiple .Keyboard .Mobile .Columns
  NxFieldFrame.razor                        # label/box/prefix/suffix/clear chrome shared by the text inputs
  NxCalendar.razor                          # the calendar grid of the date pickers, also public
  NxPickerSheet.razor                       # full-screen phone sheet shared by the pickers
  DateText.cs, DateConversion.cs, TimeConversion.cs  # internal: edit patterns, masks, parsing, TValue conversion
  Nx*.cs                                    # one public enum or record per file
  FieldValidation.cs                        # internal: EditContext plumbing shared by inputs
  wwwroot/nx-theme.css                      # ALL tokens (--nx-*), dark theme, shared .nx-spinner, scroll lock
  wwwroot/nx-popup.js, nx-combobox.js, nx-picker.js, nx-window.js, nx-tabstrip.js, nx-splitter.js, nx-tooltip.js, nx-menu.js, nx-colorpicker.js, nx-colorgradient.js, nx-mediaquery.js, nx-upload.js, nx-toolbar.js, nx-tilelayout.js, nx-chart.js, nx-input.js  # the only JS modules
samples/Nx.Demo/Data/DemoCatalog.cs          # site map: nav tree, home cards, page headers, prev/next
samples/Nx.Demo/Components/Pages/{Component}/ # Overview, Examples, Playground, Api — routes /{slug}/{page}
tests/Nx.Components.Tests/                  # one bUnit fixture per component
tests/Nx.Playwright/                        # one Playwright fixture per component
```

## Conventions

- **Parameters**: plain names as in NxGrid (`Disabled`, `Placeholder`, `Class`, `Style`); one-line XML doc on every public
  member (`GenerateDocumentationFile` is on, so a missing doc is a warning). `CaptureUnmatchedValues` goes to the
  focusable element (the `<button>`/`<input>`), `Class`/`Style` to the outer element.
- **CSS classes**: flat kebab-case with the component prefix: `nx-button`, `nx-button-primary`, `nx-combobox-item-active`.
  States are suffixes (`-active`, `-disabled`, `-invalid`), not BEM `--`.
- **Tokens**: every value a component reads is a `--nx-*` property declared in `nx-theme.css`. Scoped CSS holds layout
  only and references tokens. New token? Add it to both the light `:root` block and `.nx-theme-dark` if it is a color.
  Content passed in by the consumer (icons, templates) needs `::deep` to be styled from scoped CSS.
- **JS interop only where unavoidable**: measuring the viewport, per-key `preventDefault`, scrolling. Focus uses
  `ElementReference.FocusAsync()`. Outside-click is handled by blur (the popup cancels `mousedown`); only
  NxDateRangePicker, whose focus moves between two inputs, uses nx-popup.js's outside-press callback.
- **Popups are `position: fixed` and placed by `nx-popup.js`.** Never render a `style` attribute on a popup element.
  Phone sheets lock page scroll with the counted `lockScroll`, so nested sheets unlock only when the last closes.
- **Inputs bind with `@bind:event="oninput"` + `@bind:after`**, never `value=` + `@oninput`. Only `@bind` marks the
  event as updating `value`, which stops a slow render from writing stale text over keystrokes on Blazor Server.
- **Validation**: inputs take `[CascadingParameter] EditContext` + `ValueExpression` through `FieldValidation`;
  they render Blazor's `valid`/`invalid`/`modified` classes and `aria-invalid`. Messages come from the built-in
  `ValidationMessage`.
- **Two-way params**: only an *outside* change to `Value` replaces what the user is editing. Track the last seen
  parameter, not the last value raised.

## Checklist for Every Change

- Demo pages updated: Examples has a section for the feature, the Playground has a live control for every parameter,
  API lists every parameter, event and CSS variable. A new component gets four pages, a `DemoCatalog` entry, and its
  slug in `SiteLayoutTests.Components`. The demo is shown to people — it must look finished.
- Every page works at 390, 768 and 1280 px: no horizontal page scroll (tables sit in `.doc-table-wrap`, code in
  `.doc-code`), 44 px touch targets, examples one per row on a phone. `SiteLayoutTests` checks it.
- bUnit fixture: rendering, parameters, events, keyboard, accessibility attributes.
- Playwright fixture: real-browser behavior (focus, keyboard defaults, layout, positioning, timing).
- README feature table when a component gains a headline feature. Open decisions go in `QUESTIONS.md`.

## Testing Notes

- bUnit fixtures inherit `BunitContext` and use `[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]` — NUnit otherwise
  shares one context (and its recorded JS invocations) across every test in the fixture.
- Use `WaitForState`, not `WaitForAssertion`: NUnit records each failed `Assert` inside the retry loop as a failure.
- An un-awaited event dispatch (debounce tests) must find and dispatch inside `cut.InvokeAsync`, because every render
  issues new handler ids.
- Tests set up the JS modules in Loose mode: `nx-combobox.js` for NxComboBox, `nx-picker.js` (+ `nx-input.js` for the
  calendar) for the pickers. A bUnit `onchange` alone does not update `@bind` text; dispatch `oninput` first.
- Playwright: wait for `.layout-container[data-interactive=true]` before interacting — pages are prerendered.
  Each fixture's `GoToPage("examples" | "playground" | …)` opens `/{slug}/{page}`. `Expect(...).ToPassAsync` does
  not exist in .NET; use `WaitForFunctionAsync` or a retrying `Expect(locator)` assertion. `CountAsync` does not retry.
  Locate sections with `.doc-section` filtered by its `h2`, as in NxGrid. Playwright treats `aria-disabled` as
  disabled, so clicking a busy `NxButton` needs `Force = true`. Programmatic focus puts the caret at the start.

## Component Patterns

- **Child items** (`NxRadioButton`, `NxToggleGroupItem`, `NxComboBoxColumn`, `NxTab`, `NxSplitterPane`) find their parent through a cascading
  value and register with it. The parent calls `Refresh()` on items after it renders, because an item whose own
  parameters did not change is not re-rendered by Blazor. Generic parents carry `[CascadingTypeParameter]`.
- **"Was this parameter passed?"** — for a non-nullable `TValue`, `Min="0"` and no `Min` look the same; read it in
  `SetParametersAsync` (`NxNumericTextBox`). Keep the last state when the parameter is not passed, as Blazor does.
- **Pickers keep focus in their input** while the popup is open and forward keys to `NxCalendar.HandleKeyAsync`
  (aria-activedescendant points into the grid). The phone sheet moves focus into it and returns it to the toggle.
- **NxWindow's JS state is keyed by the window id**, not the element: Blazor's element reference is dead once the
  window leaves the DOM, and closing still has to release the scroll lock and return focus. Window keys must keep
  bubbling (Blazor listens at the document); nested windows mark the event instead of stopping it.
- **NxTab redraws its strip only when a shown value changes** (Id, Title, Disabled, Closable, whether it has an icon or
  template). Render fragments are new objects on every render; comparing them would re-render the strip forever.
- **NxSplitterPane renders the split bar before itself** (not the splitter), so panes stay in place in the markup.
  A drag sets the pane's inline style in JS, then .NET writes the same size back through `@bind-Size`.
- **Stacking**: `--nx-z-window` (900) < `--nx-z-popup` (1000) < `--nx-z-overlay` (1100), so popups and phone sheets
  open above any window.
- **NxDropDown and NxMultiSelect are NxComboBox** with a cascaded internal `ComboMode` (`NxComboBox.Modes.cs`):
  pick-only + type-ahead, the default row, select-all, max selections, the summary. No mode = the plain combo box.
  Special rows (default, select-all) have no item: test rows with `IsItemRow` before reading `row.Item`.
- **Menus**: `NxMenu` renders the whole tree (submenus `hidden`); nx-menu.js opens submenus and moves focus, Blazor
  handles the click. NxContextMenu, NxDropDownButton and NxSplitButton only decide when and where it opens.
- **NxAutoComplete is NxComboBox too**: `AllowCustomValue` plus `ComboMode.Suggest` (no arrow button, nothing highlighted until arrowed
  to, closes when nothing matches, Escape keeps the text, blur commits typed text even after the list closed).
- **NxForm wraps EditForm**: the `<form>` is EditForm's element, out of scoped CSS's reach, so layout lives on `.nx-form-body`
  (a size container: grids fold to one column under 540 px of form width) and `form.nx-form` gets its width in nx-theme.css.
  `NxFormItem` generates editors by reflection (`FormEditors.cs`), binding through a built `ValueExpression`.
- **NxIcons is generated** from Lucide SVGs (ISC; notice in the file), one property per Telerik icon name ERP uses.
- **NxButtonGroup cascades defaults to NxButton**: a button uses the group's Variant/Size only when it did not pass its own
  (read in `SetParametersAsync`, as NxNumericTextBox does for Min); Disabled adds up. Joined look is `::deep` CSS on the group.
- **NxNotification is driven from code** (`@ref` + `Show`), safe from any thread through `InvokeAsync`; timers restart after
  hover or focus leaves a message.
- **NxDialogService needs one `<NxDialogHost />`** in the layout; the host renders each open dialog in a cascading
  `NxDialogReference` (content closes it with `Close`). Its tasks complete only when the dialog closes, so in bUnit start
  them outside `cut.InvokeAsync` and `WaitForState`. NxDialog is NxWindow with `FullScreenOnPhone=false`.
- **NxMaskedTextBox types in JS** (nx-input.js `editMask` on `beforeinput`): .NET only formats and extracts (`MaskPattern`).
- **NxUpload keeps the File objects in nx-upload.js** and sends them with XHR; .NET holds the list, checks and events.
  NxFileSelect and NxUpload share `FileRules`, `NxFileDropZone` and `NxFileRow`.
- **NxTreeView rebuilds its nodes on every parameter set** (ERP edits items in place) and tracks outside changes to
  `ExpandedItems`/`SelectedItems` by reference, like the two-way rule above.
- **NxTileLayout draws its tiles from the registered items**, in its own order (as NxTabStrip draws panels); items tell it only
  when a shown value changes. NxToolBarButton renders in place; nx-toolbar.js reports the keys that do not fit and the
  bar builds the More menu from those buttons. Both use the child-item pattern above.
- **A size container needs a definite width**: NxToolBar (`contain: inline-size`) and NxTileLayout (`container-type`)
  set `width: 100%`, because a column flexbox otherwise measures them at zero width (the tiles stacked, the bar wrapped).
- **NxChart draws in NxChartSurface**, a later sibling of the series in its markup: it renders after they took their new
  parameters in the same batch, so it rebuilds the model as it renders, never in `OnParametersSet` (that runs while
  NxChart diffs, a step too early). Layout is .NET (`ChartModel`, the surface); nx-chart.js only measures and hit-tests.
  Razor reads a bare `<text>` in a loop as its own tag, so SVG texts are built in code and styled through `::deep`.
- **NxComboBox opt-in modes** (`Multiple`, columns, mobile overlay) must never change single-select, single-column
  behavior. bUnit covers both paths.

## Package Management

Versions live in `Directory.Packages.props`; reference packages in `.csproj` without a version. .NET 10 only.
