# Nx Components

Blazor UI components for the Northeast Precast ERP portal, built to replace Telerik one control at a time.
.NET 10, no third-party dependencies, themed entirely by CSS custom properties.

| Component | What it does |
|---|---|
| `NxButton` | Native button: primary/secondary/danger/ghost/link variants, three sizes, icon slot and icon-only mode, loading spinner, busy-click guard, submit/reset types, full width. |
| `NxTextBox` | Text input: `@bind-Value` on change or as you type (with debounce), label or floating label, prefix/suffix slots, clear button, password toggle, max length with counter, read-only/disabled, `EditForm` validation, autofocus, input modes. |
| `NxNumericTextBox<TValue>` | Number input for int/long/short/decimal/double/float, nullable or not: min/max/step, spin buttons with press-and-hold, arrow and page keys, .NET format strings and culture-aware parsing, decimals, prefix/suffix, select-on-focus, clear, validation. |
| `NxComboBox<TItem, TValue>` | Filterable dropdown: `Data` or async `DataProvider`, contains/starts-with/custom filtering, virtualization, keyboard navigation, custom values, clear, templates, grouping, loading state, validation, popup that flips at the viewport edge, `@bind-Value`/`@bind-SelectedItem`. Opt-in **multi-column** grid list, **multi-select** chips (`@bind-Values`/`@bind-SelectedItems`), and a full-screen **mobile overlay** below 600 px. |
| `NxDropDown<TItem, TValue>` | Pick-only list on the NxComboBox engine: no typing, type-ahead by first letters, a default ("none") item, same data sources, templates, grouping, virtualization, columns, phone sheet and validation. |
| `NxMultiSelect<TItem, TValue>` | Multi-select on the same engine as NxComboBox `Multiple` (shared code): chips or a "3 selected" summary, filter box, select-all, max selections, pick-only mode, phone sheet that applies on Done. |
| `NxAutoComplete<TItem>` | A text box that suggests, on the NxComboBox engine: the value is the typed text; matches open after `MinLength` characters (contains/starts-with/custom), arrow-and-Enter or click picks one, Enter and leaving commit free text; item template, prefix icon, `DataProvider`, virtualization, validation. |
| `NxTextArea` | Multi-line text in the NxTextBox field: label/floating label, counter, clear, validation, debounce; rows, auto-resize between min and max rows, resize handle. |
| `NxDatePicker<TValue>` | Date input for `DateOnly`/`DateTime`, nullable or not: masked typing in the culture's order, any display `Format`, calendar popup with month/year/decade views, min/max, disabled-dates callback, week numbers, first day of week, Today, clear, validation; a full-screen sheet on phones. |
| `NxDateRangePicker<TValue>` | Start and end dates (`@bind-StartValue`/`@bind-EndValue`) on one calendar, two months side by side on a desktop, start-then-end picking with the range band, the date picker's typing and limits, a phone sheet. |
| `NxTimePicker<TValue>` | Time input for `TimeOnly`/`TimeSpan`/`DateTime`: 12- or 24-hour, loose typing (`930p`, `1430`), a list every `Step` minutes between min and max, clear, validation; a phone sheet. |
| `NxDateTimePicker<TValue>` | Date and time in one field for `DateTime`/`DateTime?`: any .NET format (all-number 24-hour ones masked, others read leniently), a popup with the calendar beside a list of times every `TimeStep` minutes, min/max as date-times, clear, validation; a phone sheet with Date and Time tabs. |
| `NxCalendar` | The pickers' calendar on its own: keyboard grid, month/year/decade views, ranges, one or two months. |
| `NxForm` + `NxFormItem` + `NxFormGroup` | An `EditForm` laid out in a grid: `Columns`/`ColSpan`, titled groups, labels above or beside, one column on narrow forms (container query). An item names a model property and generates its editor (text, number, check box, date, time, enum) with label and messages, or wraps your own editor. DataAnnotations by default, any validators in `Validation`, `IsValid()`, `SubmitOnEnter`. |
| `NxValidationMessage` | One field's messages (`For` lambda or `Field` name) with an icon, in a live region. |
| `NxWindow` | Dialog window: `@bind-Visible`, modal or not, title with icon, close / minimize / maximize, drag by the title bar, resize from the edges, size presets or explicit size and position, focus trap and focus return, Escape and backdrop close, a cancelable `OnClosing`, stacking of nested windows, body and footer slots; full screen on phones. |
| `NxTabStrip` + `NxTab` | Tabs over panels: text, icon or template headers, disabled and closable tabs (`OnTabClose`), lazy or `KeepAlive` panels, `@bind-ActiveTab`, tabs top/bottom/left/right, overflow arrows, ARIA tablist keyboard with automatic activation. |
| `NxSplitter` + `NxSplitterPane` | Resizable panes, horizontal or vertical: sizes in px or % with `@bind-Size`, min/max, collapsible panes with `@bind-Collapsed`, drag and keyboard resizing, nesting; panes stack with collapse headers on phones. |
| `NxTooltip` | Tooltip for a wrapped or targeted element: hover, keyboard focus, click or long press; delays; placement with flip and an arrow; rich content; never takes pointer events; `aria-describedby`. |
| `NxMenu` / `NxMenuItem` | The menu behind the next three: icons, shortcut text, separators, disabled and check items, nested submenus, keyboard, flipping, phone bottom sheet with drill-in. |
| `NxContextMenu` | Right-click and long-press menu on a wrapped or targeted element, or opened from code; `data-nx-context` tells which row was pressed. |
| `NxDropDownButton` / `NxSplitButton` | An NxButton that opens a menu; a primary action joined to a menu arrow. Every NxButton variant and size. |
| `NxIcon` + `NxIcons` | Inline SVG icons: every one ERP uses (131), named after the Telerik SvgIcons they replace (Lucide, ISC), seven sizes, theme colors, custom `NxIconData`, decorative or labelled. |
| `NxLoader` / `NxLoaderContainer` | Spinner, converging or pulsing dots in three sizes; an overlay with a loader (and text or your content) that covers its positioned parent and blocks clicks. |
| `NxCheckBox<TValue>` | Check box for `bool` or `bool?` (indeterminate), three-state cycling, label on either side, read-only, validation. |
| `NxToggleSwitch` | `role="switch"` on/off toggle: on/off text inside or beside, three sizes, loading state, validation. |
| `NxRadioGroup<TValue>` + `NxRadioButton<TValue>` | One choice from `Data` or declared buttons, vertical or horizontal, disabled items, item template, native-radio roving tab stop, validation. |
| `NxToggleGroup<TValue>` + `NxToggleGroupItem<TValue>` | Segmented buttons: single select (radio group) or `Multiple` (toggle buttons), icons and icon-only, sizes, full width, disabled items, roving tabindex, validation. |

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
