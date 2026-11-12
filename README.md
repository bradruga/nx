# Nx Components

Blazor UI components for the Northeast Precast ERP portal, built to replace Telerik one control at a time.
.NET 10, no third-party dependencies, themed entirely by CSS custom properties.

| Component | What it does |
|---|---|
| `NxButton` | Native button: primary/secondary/danger/ghost/link and success/warning/info variants, three sizes, icon slot and icon-only mode, loading spinner, busy-click guard, submit/reset types, full width. |
| `NxButtonGroup` | Joins NxButtons (and menu buttons) into one bar of related actions; `Variant`, `Size` and `Disabled` on the group apply to every button that does not set its own; full width; `role="group"`. |
| `NxToggleButton` | A button that stays on or off: `@bind-Selected`, `aria-pressed`, a pressed look in every NxButton variant, icons and icon-only, works inside NxButtonGroup. |
| `NxToolBar` + `NxToolBarButton` | A row of actions with separators and a spacer: buttons that do not fit move into a More menu (or the bar wraps or scrolls), one tab stop with arrow keys, `Visible`/`Disabled` per button, the toolbar's size for every button. |
| `NxTextBox` | Text input: `@bind-Value` on change or as you type (with debounce), label or floating label, prefix/suffix slots, clear button, password toggle, max length with counter, read-only/disabled, `EditForm` validation, autofocus, input modes. |
| `NxNumericTextBox<TValue>` | Number input for int/long/short/decimal/double/float, nullable or not: min/max/step, spin buttons with press-and-hold, arrow and page keys, .NET format strings and culture-aware parsing, decimals, prefix/suffix, select-on-focus, clear, validation. |
| `NxSlider<TValue>` | A number between `Min` and `Max` on a native range input: filled track, labelled ticks every `LargeStep`, − / + buttons, the value beside it, raised while dragging or on release, any number type, validation. |
| `NxComboBox<TItem, TValue>` | Filterable dropdown: `Data` or async `DataProvider`, contains/starts-with/custom filtering, virtualization, keyboard navigation, custom values, clear, templates, grouping, loading state, validation, popup that flips at the viewport edge, `@bind-Value`/`@bind-SelectedItem`. Opt-in **multi-column** grid list, **multi-select** chips (`@bind-Values`/`@bind-SelectedItems`), and a full-screen **mobile overlay** below 600 px. |
| `NxDropDown<TItem, TValue>` | Pick-only list on the NxComboBox engine: no typing, type-ahead by first letters, a default ("none") item, same data sources, templates, grouping, virtualization, columns, phone sheet and validation. |
| `NxMultiSelect<TItem, TValue>` | Multi-select on the same engine as NxComboBox `Multiple` (shared code): chips or a "3 selected" summary, filter box, select-all, max selections, pick-only mode, phone sheet that applies on Done. |
| `NxAutoComplete<TItem>` | A text box that suggests, on the NxComboBox engine: the value is the typed text; matches open after `MinLength` characters (contains/starts-with/custom), arrow-and-Enter or click picks one, Enter and leaving commit free text; item template, prefix icon, `DataProvider`, virtualization, validation. |
| `NxTextArea` | Multi-line text in the NxTextBox field: label/floating label, counter, clear, validation, debounce; rows, auto-resize between min and max rows, resize handle. |
| `NxColorPicker` | Picks a CSS color from a palette grid (the Office palette ERP uses, or your own `Colors` and `Columns`): arrow keys by cell and row, "No color", the value's text, validation. |
| `NxColorGradient` | Any color: a saturation/brightness area to drag or arrow through, hue and opacity sliders, HEX or RGB(A) inputs, a preview; hex or rgb() values; shares NxColorPicker's color parsing. |
| `NxMaskedTextBox` | Text in a fixed pattern (`00-0000`, `(000) 000-0000`, `LL-000-A`): Telerik's mask rules, a prompt character, the mask always or on focus, value with or without literals, number pad for digit masks, clear, validation. |
| `NxFileSelect` | Picks files to read in .NET (Blazor's InputFile): a click-or-drop zone, type and size checks, a list with remove buttons, `OnSelect` with each file's stream. |
| `NxUpload` | Sends files to a URL from the browser: drop, pick or paste, type and size checks, a progress bar per file, cancel, retry, remove, concurrency limit, upload now or on demand, the server's answer in `OnSuccess`. |
| `NxDatePicker<TValue>` | Date input for `DateOnly`/`DateTime`, nullable or not: masked typing in the culture's order, any display `Format`, calendar popup with month/year/decade views, min/max, disabled-dates callback, week numbers, first day of week, Today, clear, validation; a full-screen sheet on phones. |
| `NxDateInput<TValue>` | The date picker's masked field with no calendar: culture-ordered typing, Up / Down step the day, month or year under the caret, any display `Format`, min/max, clear, validation. |
| `NxDateRangePicker<TValue>` | Start and end dates (`@bind-StartValue`/`@bind-EndValue`) on one calendar, two months side by side on a desktop, start-then-end picking with the range band, the date picker's typing and limits, a phone sheet. |
| `NxTimePicker<TValue>` | Time input for `TimeOnly`/`TimeSpan`/`DateTime`: 12- or 24-hour, loose typing (`930p`, `1430`), a list every `Step` minutes between min and max, clear, validation; a phone sheet. |
| `NxDateTimePicker<TValue>` | Date and time in one field for `DateTime`/`DateTime?`: any .NET format (all-number 24-hour ones masked, others read leniently), a popup with the calendar beside a list of times every `TimeStep` minutes, min/max as date-times, clear, validation; a phone sheet with Date and Time tabs. |
| `NxCalendar` | The pickers' calendar on its own: keyboard grid, month/year/decade views, ranges, one or two months. |
| `NxForm` + `NxFormItem` + `NxFormGroup` | An `EditForm` laid out in a grid: `Columns`/`ColSpan`, titled groups, labels above or beside, one column on narrow forms (container query). An item names a model property and generates its editor (text, number, check box, date, time, enum) with label and messages, or wraps your own editor. DataAnnotations by default, any validators in `Validation`, `IsValid()`, `SubmitOnEnter`. |
| `NxValidationMessage` | One field's messages (`For` lambda or `Field` name) with an icon, in a live region. |
| `NxWindow` | Dialog window: `@bind-Visible`, modal or not, title with icon, close / minimize / maximize, drag by the title bar, resize from the edges, size presets or explicit size and position, focus trap and focus return, Escape and backdrop close, a cancelable `OnClosing`, stacking of nested windows, body and footer slots; full screen on phones. |
| `NxTileLayout` + `NxTileLayoutItem` | A dashboard of titled tiles on a grid of `Columns` and `RowHeight` rows with col/row spans: drag a header to reorder, pull a corner to resize, Ctrl/Shift+arrows from the keyboard, `GetState`/`SetState`, one column when narrow. |
| `NxDialog` + `NxDialogService` | A short modal card in markup (`@bind-Visible`, title, content, buttons), and a service for `AlertAsync`, `ConfirmAsync`, `PromptAsync`, `ShowAsync(options)` with validation and a busy save, `Open` + `Close` from code, and a progress dialog; stays a card on phones. |
| `NxMediaQuery` | A CSS media query as a bindable bool (`@bind-Matches`) that follows resizes, pointer and color scheme. |
| `NxTabStrip` + `NxTab` | Tabs over panels: text, icon or template headers, disabled and closable tabs (`OnTabClose`), lazy or `KeepAlive` panels, `@bind-ActiveTab`, tabs top/bottom/left/right, overflow arrows, ARIA tablist keyboard with automatic activation. |
| `NxSplitter` + `NxSplitterPane` | Resizable panes, horizontal or vertical: sizes in px or % with `@bind-Size`, min/max, collapsible panes with `@bind-Collapsed`, drag and keyboard resizing, nesting; panes stack with collapse headers on phones. |
| `NxTooltip` | Tooltip for a wrapped or targeted element: hover, keyboard focus, click or long press; delays; placement with flip and an arrow; rich content; never takes pointer events; `aria-describedby`. |
| `NxMenu` / `NxMenuItem` | The menu behind the next three: icons, shortcut text, separators, disabled and check items, nested submenus, keyboard, flipping, phone bottom sheet with drill-in. |
| `NxContextMenu` | Right-click and long-press menu on a wrapped or targeted element, or opened from code; `data-nx-context` tells which row was pressed. |
| `NxDropDownButton` / `NxSplitButton` | An NxButton that opens a menu; a primary action joined to a menu arrow. Every NxButton variant and size. |
| `NxIcon` + `NxIcons` | Inline SVG icons: every one ERP uses (131), named after the Telerik SvgIcons they replace (Lucide, ISC), seven sizes, theme colors, custom `NxIconData`, decorative or labelled. |
| `NxLoader` / `NxLoaderContainer` | Spinner, converging or pulsing dots in three sizes; an overlay with a loader (and text or your content) that covers its positioned parent and blocks clicks. |
| `NxProgressBar` | `Value` of `Max` with a centered label readable over fill and track, a `LabelTemplate`, a `Thin` bar, an `Indeterminate` one; ARIA progressbar values. |
| `NxChipList<TItem>` | A wrapping row of chips from `Data`, text or `ItemTemplate`, removable with a button or Delete/Backspace, arrow keys between chips, solid or outline, three sizes. |
| `NxCarousel<TItem>` | One slide at a time from `Data` and a `Template`, rotating every `AutomaticPageChangeInterval` ms and pausing on hover, focus or the pause button; arrows, dots, swipe, arrow keys, `@bind-Page`. |
| `NxChart` + `NxChartSeries` | Line, area, column, bar and pie charts drawn as SVG by .NET, no chart library: Telerik-style `Data` + `Field`/`CategoryField` binding, date category axes with hour-to-year steps and aggregates, two value axes, stacking, dashed lines, value labels, a legend that hides series, tooltips by hover, tap or arrow keys, `OnSeriesClick`, redraws as its container resizes. |
| `NxEditor` | Rich text bound as HTML with `@bind-Value` (`""` when empty): a tool list in Telerik's names — text styles, formats, fonts, sizes, text and highlight colors, alignment, lists, links, pictures, tables with row/column edits, merge and split — custom tools in their own slots, its own undo history, Word and web paste cleaned (scripts, classes, Office styles; Word lists made lists), `InsertHtmlAsync`, validation. |
| `NxChat<TItem>` | A conversation read from `Data` by field name as Telerik's chat: the user's messages right, others' with avatar, name and time, grouped by author and day, typing dots, a `MessageContentTemplate`, Enter to send, and a list that follows new messages unless the user scrolled up. |
| `NxNotification` | Toasts stacked in a corner: `` + `Show(model)` with Info/Success/Warning/Error/Neutral, `CloseAfter` held by hover or focus, close button, six positions, `MaxVisible`, a live region (errors as alerts); full width on phones. |
| `NxCheckBox<TValue>` | Check box for `bool` or `bool?` (indeterminate), three-state cycling, label on either side, read-only, validation. |
| `NxToggleSwitch` | `role="switch"` on/off toggle: on/off text inside or beside, three sizes, loading state, validation. |
| `NxRadioGroup<TValue>` + `NxRadioButton<TValue>` | One choice from `Data` or declared buttons, vertical or horizontal, disabled items, item template, native-radio roving tab stop, validation. |
| `NxToggleGroup<TValue>` + `NxToggleGroupItem<TValue>` | Segmented buttons: single select (radio group) or `Multiple` (toggle buttons), icons and icon-only, sizes, full width, disabled items, roving tabindex, validation. |
| `NxTreeView<TItem>` | A tree from flat Id/ParentId data or nested children: expand, single or multiple selection with `@bind`, item template, ARIA tree keyboard, drag and drop before, onto or after items. |

## Layout

```
Nx.slnx
src/Nx.Components/          Razor class library — every component
  wwwroot/nx-theme.css      The one token/theme file (light + .nx-theme-dark)
  wwwroot/nx-popup.js       Shared JS: popup placement, outside press, viewport watch, scroll lock
  wwwroot/nx-combobox.js    Combo box JS: key handling, scrolling the active option
  wwwroot/nx-picker.js      Date/time picker JS: keys the open popup takes over
  wwwroot/nx-window.js      Window JS: placement, drag, resize, stacking, focus trap, Escape, focus return
  wwwroot/nx-tabstrip.js    Tab strip JS: arrow-key focus and activation, overflow arrows
  wwwroot/nx-splitter.js    Splitter JS: drag and keyboard resizing of the split bars
  wwwroot/nx-tooltip.js     Tooltip JS: triggers, delays, placement, long press, aria-describedby
  wwwroot/nx-menu.js        Menu JS: placement, submenus, keyboard, dismissal, right click and long press
  wwwroot/nx-input.js       Shared JS: per-key preventDefault, roving focus, input masks, select-on-focus
  wwwroot/nx-colorpicker.js Color picker JS: palette grid keys, focus on open, close on Tab out
  NxFieldFrame.razor        Shared label/box/prefix/suffix/clear chrome of every text-style input
  NxPickerSheet.razor       Shared full-screen phone sheet of the pickers
  NxButton.razor(.cs/.css)  One component = markup + code-behind + scoped CSS
samples/Nx.Demo/            Blazor Web App (interactive server): Overview, Examples, Playground and API
                            pages per component at /{component}/{page}, responsive down to 390 px
tests/Nx.Components.Tests/  bUnit + NUnit, one fixture per component
tests/Nx.Playwright/        Playwright .NET + NUnit against the demo site, one fixture per component
```

## Run the demo

```powershell
dotnet run --project samples/Nx.Demo
```

The demo listens on **all interfaces, port 44420** (`http://0.0.0.0:44420`, set in
`samples/Nx.Demo/Properties/launchSettings.json`), so it is reachable from other machines on the network:

- this machine: <http://localhost:44420>
- anyone else: `http://<this computer's hostname or IP>:44420` — port 44420 must be open in the Windows firewall.

HTTP only; there is no HTTPS profile, because the dev certificate only covers `localhost`.

In a Treeline worktree, bring it up with Treeline's `start_watch` instead of `dotnet run`.

## Run the tests

```powershell
# Component tests (bUnit) — no app needed
dotnet test tests/Nx.Components.Tests

# End-to-end tests (Playwright) — the demo must be running first
pwsh tests/Nx.Playwright/bin/Debug/net10.0/playwright.ps1 install chromium   # once, after the first build
dotnet test tests/Nx.Playwright
```

Playwright targets `http://localhost:44420`. Point it elsewhere with `NX_DEMO_URL`:

```powershell
$env:NX_DEMO_URL = "http://build-box:44420"; dotnet test tests/Nx.Playwright
```

## Use the components

```xml
<ProjectReference Include="..\Nx.Components\Nx.Components.csproj" />
```

```html
<!-- App.razor <head> -->
<link rel="stylesheet" href="@Assets["_content/Nx.Components/nx-theme.css"]" />
<link rel="stylesheet" href="@Assets["YourApp.styles.css"]" />  <!-- carries the components' scoped CSS -->
```

```razor
@using Nx.Components

<EditForm Model="order" OnValidSubmit="SaveAsync">
    <DataAnnotationsValidator />

    <NxComboBox Data="customers" ItemText="c => c.Name" ItemValue="c => c.Id"
                @bind-Value="order.CustomerId" Placeholder="Customer" Clearable="true" />
    <ValidationMessage For="() => order.CustomerId" />

    <NxTextBox Label="PO number" @bind-Value="order.PoNumber" MaxLength="20" ShowCounter="true" />
    <ValidationMessage For="() => order.PoNumber" />

    <NxButton Type="NxButtonType.Submit" Variant="NxButtonVariant.Primary">Save</NxButton>
</EditForm>
```

For `NxDialogService`, register it and put one host in the layout:

```csharp
builder.Services.AddNxDialogs();          // Program.cs
```

```razor
<NxDialogHost />                          @* MainLayout.razor, inside the interactive layout *@
```

## Theming

Every color, radius and size is a custom property in `nx-theme.css`; override any of them on `:root` or on a
container. `class="nx-theme-dark"` on `<html>` (or any element) switches to the dark palette.

```css
:root {
    --nx-accent: #16A34A;
    --nx-accent-hover: #15803D;
    --nx-radius: 4px;
}
```

Open decisions are in [QUESTIONS.md](QUESTIONS.md).
