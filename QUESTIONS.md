# Open Questions

Decisions I made without you. Each one says what I picked and why, so you can answer by number.
1–23 are from the first pass (all accepted); 24 onward are new.

## Blocking

1. **Playwright tests can't run under Treeline.** While a watch is live, the Treeline hook blocks every
   `dotnet test tests/Nx.Playwright`, including `--no-build` and the bare DLL: it classifies the project as one that
   builds the watched app. It references nothing. With the watch stopped there's no site to test. I did not work
   around the hook. All 76 Playwright tests compile, and I checked each scenario by hand in the Playwright browser
   against the live demo, but **none has gone through `dotnet test`**. Options:
   - (a) Treeline lists `Nx.Playwright` as a free test project (recommended).
   - (b) You run `dotnet test tests/Nx.Playwright` with the demo up and paste the output.

## Conventions

2. **NUnit, not xUnit.** NxGrid's CLAUDE.md says "bUnit with xUnit", but its test projects reference NUnit. I followed
   the projects.
3. **.NET 10 only.** NxGrid's library also targets net8.0. You said .NET 10, and the ERP is on 10, so I dropped net8.
4. **No SCSS.** NxGrid keeps SCSS plus a hand-synced CSS copy. Here it's one plain `nx-theme.css` plus scoped `.razor.css`
   per component, as you asked.
5. **Accent color.** The library default is NxGrid's blue (`#0078d4`). The demo overrides it with NxGrid's green
   site brand. Should the ERP default be blue, green, or the portal's own color?
6. **Demo HTTPS.** Off, as you said. NxGrid's sample has an https profile, but the dev certificate only covers
   `localhost`, so other machines would get a certificate error on 44420.

## NxButton

7. **Loading uses `aria-disabled`, not `disabled`.** Focus stays on the button while it saves, and clicks and submits
   are blocked by `preventDefault`. `Disabled` is the native attribute.
8. **`PreventDoubleClick` is on by default.** Every async click is guarded. `AutoLoading` (spinner while the handler
   runs) is off by default. Should it be on for the ERP?
9. **Default `Type` is `Button`, not the HTML default `submit`.** It avoids accidental form posts.

## NxTextBox

10. **Validation is hand-rolled, not `InputBase<string>`.** `InputBase` forces string parsing on the combo box's
    generic `TValue`. The plumbing is about 40 lines, shared in `FieldValidation`. Its CSS classes and
    `NotifyFieldChanged` match `InputBase`.
11. **Clearing sets `""`, not `null`.** That's the same value as deleting the text by hand. Say if the ERP models
    treat null and empty differently.
12. **`ShowCounter` is off by default**, even with `MaxLength` set. `MaxLength` alone is common for DB column
    limits.
13. **The clear button is out of the tab order; Esc clears.** The password toggle stays tabbable.
14. **No size parameter.** Text boxes are 34px (`--nx-control-height-md`), the same as a Medium button. Floating-label
    fields are taller (48px).

## NxComboBox

15. **Names: `ItemText`, `ItemValue`, `ItemGroup`.** Alternatives:
    - (a) `TextField`/`ValueField`/`GroupField`, Telerik's names. Easier migration, but they sound like property-name strings.
    - (b) `TextSelector`/`ValueSelector`. Precise, but long.
    - (c) Current. Short, and reads as "the item's text".
16. **`DataProvider` returns the full filtered list; there's no server paging.** Virtualization works on what comes
    back. Do the ERP's large lookups (parts, jobs) need start/count paging?
17. **With `DataProvider`, a `Value` alone can't show its text** until the list loads. Bind `@bind-SelectedItem` too,
    or tell me if you want an `ItemByValue` lookup callback.
18. **`AllowCustomValue` requires `TValue` = `string`.** Other types throw. A converter parameter could lift that.
19. **Leaving the field with typed text:** an exact text match selects that item, else a custom value (when allowed),
    else the highlighted match, else it reverts. With custom values on, Enter keeps the typed text unless you arrowed
    to an item. That's my best guess at fast ERP data entry; the Telerik reference may differ.
20. **Empty text on blur clears the value**, even without `Clearable`.
21. **The popup prefers below.** It flips above only when there isn't room below and there's more above. It's
    `position: fixed`, so it escapes `overflow: hidden` and dialogs, but a `transform` on an ancestor would break that.
22. **Group headers are `role="presentation"`.** A screen reader doesn't announce the group name; nesting options in
    `role="group"` would conflict with virtualization.
23. **Setting `Value` from code also raises `SelectedItemChanged`** (and the reverse) when both are bound, so they
    stay in step. A parent that binds one-way to both gets controlled behavior: the selection follows its parameters.

## Pass 2 — demo site

24. **Drawer below 900 px.** Tablet portrait (768) gets the drawer; landscape tablet and up get the fixed sidebar.
    A narrow icon rail at 768 would need icons per component; I skipped it.
25. **API tables scroll inside their box on a phone**, at a readable 620 px minimum, rather than reflowing to
    label/value cards. Reflow would need a `data-label` on every cell of every table.
26. **Routes are `/{slug}/{page}`**, with slugs `button`, `textbox`, `numeric`, `combobox`, `checkbox`, `switch`, `radio`,
    `togglegroup`. The bare `/{slug}` still opens Overview.

## Pass 2 — NxComboBox

27. **Mobile overlay is on by default below 600 px** (`MobileMode="Auto"`), as specified. That does change existing
    single-select combos on phones; `MobileMode="Never"` opts out. Multi-select and columns stay strictly opt-in.
28. **`@bind-Values` needs an `IEnumerable<TValue>` field**, not a `List<T>`: Blazor's bind assigns the callback's type back.
29. **Multi-select keys.** Enter toggles the highlighted item. Space toggles only after you have arrowed to an item,
    so you can still type a space while filtering. The typed filter is kept after a toggle, so several matches can be
    picked in a row.
30. **The overlay's multi-select is staged.** Toggles apply on Done; back and Escape discard them. On a desktop popup,
    toggles apply at once.
31. **`AllowCustomValue` is not available with `Multiple`** (it throws). Custom chips would need a converter.
32. **Columns filter on every column by default** (`FilterAllColumns="true"`), plus `ItemText`. The header is
    `aria-hidden`, and options stay `role="option"` rather than becoming a `role="grid"`.

## Pass 2 — new components

33. **`NxFieldFrame` is public.** Razor cannot make a component internal; it is documented as a building block.
    NxTextBox now renders through it, with every class name unchanged.
34. **New `NxSize`** for the switch and toggle group; `NxButtonSize` stays. Merge them into one `NxSize`?
35. **NxNumericTextBox formats only while unfocused.** Focus shows the plain number. Format `"P"` shows the value ×100
    when not focused; use `Suffix="%"` with plain values instead.
36. **Numeric parsing drops the culture's group separators**, so in de-DE `1.5` reads as 15 — what Excel does. It also
    accepts currency symbols, `%` and accounting parentheses, and the no-break spaces .NET formats de-DE and fr-CA with.
37. **Numeric: emptying a non-nullable field puts the last value back**; Clear sets Min, or 0. `SelectOnFocus` is off
    by default — on for the ERP's quantity grids?
38. **Numeric `inputmode`.** It is `numeric`/`decimal` only when Min ≥ 0, because the iOS number pad has no minus key.
39. **double/float beyond ±7.9e28** (decimal's range) are not supported. Fine for the ERP, I think.
40. **Check box and switch also toggle on Enter**, because they are native buttons. ARIA specifies Space only;
    blocking Enter needs JS.
41. **`NxRadioGroup<TValue>` takes `Data` of values** (enums, strings). For records, declare `NxRadioButton`s — or should
    I add an `NxRadioGroup<TItem, TValue>` with `ItemValue`, like the combo box?
42. **Radios are native inputs**, so there is no `ReadOnly` (browsers ignore it on radios). Add one with CSS and
    blocked changes?
43. **`NxToggleGroup.AllowDeselect`** was not in the brief; I added it because a single-select group otherwise cannot
    return to empty. Its multi-select mode is `role="group"` with the same roving tabindex, the toolbar pattern.
44. **NxToggleGroup sits under Selection** in the nav, not Buttons.

## Pass 3 — date and time pickers

45. **The calendar keeps focus in the field.** While the popup is open, ← → ↑ ↓ PgUp PgDn and Enter drive the calendar
    and the caret stays put; Esc gives the arrows back. Home/End stay with the text. The ARIA dialog pattern moves focus
    into the grid instead — that is what the phone sheet does.
46. **Typed dates outside Min/Max move to the limit** (as the numeric box clamps); a typed disabled date reverts.
    Showing it as invalid instead would need the picker to hold a value it will not raise.
47. **Masking is digits-only.** All-number formats mask in their own order (`yyyy-MM-dd` gives `9999-99-99`); a
    format with names (`D`, `MMM d`) edits in the culture's short date. Two-digit years are accepted when typed with
    separators (`1/5/26`) and read with the culture's century rule.
48. **Disabled days stay focusable** (struck through, `aria-disabled`), as the ARIA grid pattern recommends, so the
    arrows do not jump over weekends.
49. **New `NxMobileMode`** for the pickers. `NxComboBoxMobileMode` has the same three values — replace it with
    `NxMobileMode`? That is a breaking rename of a pass-2 parameter type.
50. **`DateTimeOffset` is not supported** by the pickers. Add it?
51. **Range picking:** the first pick is the start, the second the end; a second pick before the start restarts there;
    a new start after the end clears the end; a typed start after the end swaps them. No hover preview of the band
    (it would cost a server round trip per mouse move).
52. **Range validation is per end**: `StartValueExpression` and `EndValueExpression` are separate fields, so a rule
    across both goes on the model (as in the demo).
53. **NxTimePicker is a list at `Step`**, not hour and minute columns. Typed times need not be on a step. Seconds are
    dropped. Should it offer a two-column spinner on phones?
54. **Time picker `DateTime` with no previous value uses today's date.**
55. **`NxCalendar` and `NxPickerSheet` are public** (Razor components cannot be internal). NxCalendar is documented on
    the NxDatePicker pages rather than having its own nav entry.
56. **New nav category "Dates"** between Inputs and Selection.

## Pass 3 — NxWindow

57. **`Modal` defaults to `true`.** Telerik's Window defaults to non-modal; ERP dialogs are mostly modal.
58. **The window renders where it is declared**, position-fixed. An ancestor with a `transform` or `filter` would
    make it relative to that ancestor. A portal (render at the layout root) needs a host component in the app layout —
    worth it?
59. **Escape closes the window unless a control used it first** (open combo list, open date picker). A clearable
    NxTextBox clears *and* the window closes on the same Escape, because the text box does not mark the key used.
    Mark it?
60. **Size and position are not reported back.** Drag and resize stay in the browser; there is no `@bind-Width` or
    position event. The ERP may want to remember a window's size — add `OnResized(width, height)`?
61. **Minimized windows stay where they were** (title bar only, 280 px wide); on phones they dock to the bottom.
    No taskbar of minimized windows.
62. **Size presets are widths only** (Small 400, Medium 600, Large 900, Auto); height follows the content up to the
    viewport unless `Height` is set.
63. **New nav category "Layout"** for NxWindow, NxTabStrip and NxSplitter.

## Pass 3 — NxTabStrip

64. **`ActiveTab` is a string key**: the tab's `Id`, else its `Title`. No `ActiveIndex` — indexes shift when tabs close.
    With no match the first enabled tab shows, without raising `ActiveTabChanged`.
65. **Arrow keys activate** (ARIA "automatic activation"). With heavy panels, manual activation (arrows move focus,
    Enter activates) is the alternative — add `ActivationMode`?
66. **Closing is the parent's job**: `OnTabClose` reports the key and the markup removes the tab. Closing the active tab
    first moves to the right neighbour, else the left. Not cancelable — add `Cancel`?
67. **`KeepAlive` keeps panels that have been shown**, not all panels up front.
68. **Left/right tabs move to the top below 600 px.** The strip fills its container's width.

## Pass 3 — NxSplitter

69. **`Size` is a CSS string** (`"30%"`, `"240px"`), not a number + unit. A resized pane reports in the unit it was
    given; a pane with no size that gets dragged starts reporting px.
70. **A bar resizes the pane before it** when that pane has a size (or neither has); otherwise the one after. Keep at
    least one pane without a size, or the panes can overflow the splitter.
71. **`NxOrientation` is shared** with NxRadioGroup; its doc comments now cover both. The splitter defaults to
    Horizontal, the radio group to Vertical.
72. **Panes register in render order.** Inserting a pane between existing ones at runtime may mis-number the bars until
    the next render; adding at the end and removing work.
73. **Phones stack every splitter** below 600 px, each pane capped at 60vh with its own scroll; panes with a `Title`
    get a header that collapses them (if `Collapsible`).

## Pass 4

74. **NxDropDown and NxMultiSelect wrap NxComboBox** and switch on modes through an *internal* cascaded `ComboMode`,
    so NxComboBox's public API is unchanged and a plain combo box behaves exactly as before. The wrappers forward each
    parameter (≈40 lines each). The alternative, subclassing NxComboBox, would have shown every combo parameter
    (AllowCustomValue, Filter…) on NxDropDown.
75. **The mode cascades** to anything inside the wrapper's templates too; a combo box placed inside a dropdown's item
    template would pick up the mode. Unlikely; say if it matters.
76. **NxDropDown's DataProvider is NxComboBox's signature** (`NxComboBoxRequest`, filter always empty), per "same
    DataProvider", and it is called each time the list opens.
77. **NxDropDown type-ahead with the list closed selects** the next match (a native select does this). Type-ahead
    prefixes reset after 0.7 s or on any non-letter key.
78. **NxMultiSelect "filter box" is the field itself** (type to filter), as in NxComboBox `Multiple`; `Filterable="false"`
    makes it pick-only with type-ahead. A separate search box inside the popup was not added.
79. **Select all selects what is listed** (after filtering), not every item; with all listed selected it clears them.
    Its text is fixed ("Select all") — add a parameter?
80. **NxMultiSelect opens like the combo box**: typing, ↓ or the arrow button; a plain click on the field only focuses
    it (pick-only mode opens on click).
81. **NxFieldFrame got a `Multiline` parameter** (shared component) for NxTextArea — a modifier class and CSS only;
    single-line inputs are unchanged. NxTextArea duplicates NxTextBox's commit/debounce logic rather than sharing a
    base class with it.
82. **Tooltips cannot be hovered** (`pointer-events: none`, as asked). WCAG 1.4.13 wants hover content to be hoverable;
    ours is dismissible (Escape) and persistent while the pointer stays on the anchor.
83. **Tooltip focus trigger is keyboard focus only** (`:focus-visible`); a mouse click does not show it. Hover is
    mouse-only, so a tap on a phone does not show it — the long press does.
84. **Menus open from .NET** (a server round trip after the right click) so `OnOpening` can change the items per row.
    Arrow keys, submenus and hover are pure JS.
85. **Context menu items do not auto-toggle `Checked`**: `OnItemClick` flips the app's state and the menu gets new items
    (shown in the demo). `NxMenuItem` is immutable.
86. **NxMenu is public** — the building block — and can be opened from code at a point or under an element.
87. **Menu buttons swallow ↑/↓** (`preventKeys` on the wrapper) so opening by keyboard does not scroll the page. A
    menu under a button follows it when the page scrolls; a menu at a point closes.
88. **New nav category "Menus & Overlays"** (Tooltip, ContextMenu); NxDropDownButton and NxSplitButton sit under
    Buttons, NxDropDown/NxMultiSelect/NxTextArea under Inputs.

## Pass 5 — from ERP's usages (read only)

89. **No item list + renderer.** ERP declares `FormItems` and then places them by index with `TelerikFormItemRenderer
    Item="formObjects[++i]"` (489 renderers) to get custom layouts. NxFormItem renders where it is written, inside any
    markup in the form, so the renderer and the index bookkeeping go away. Migration moves each item into the layout.
90. **An item's messages come with it.** NxFormItem shows its Field's messages under the editor, also around your own
    editor. ERP's templates carry their own `TelerikValidationMessage`; migration drops those (or the item would show two).
91. **Generated editors** (ERP has 194 `<FormItem Field=… />` with no template): string → NxTextBox, numbers →
    NxNumericTextBox, bool → NxCheckBox (labelled after the box), DateTime/DateOnly → NxDatePicker (a DateTime keeps its
    time; ERP never used Telerik's DateTimePicker editor there), TimeOnly/TimeSpan → NxTimePicker, enum → NxDropDown with
    the names in words. Other types throw and ask for an editor inside the item. `EditorType` (Switch ×5, TextArea ×3)
    was not added: put an NxToggleSwitch or NxTextArea inside the item.
92. **`Field` is a string, as ERP passes it** (`@nameof(…)`), resolved on the form's model. Fields of nested objects
    need your own editor inside the item. A `For` lambda would allow those; say if you want it.
93. **No default buttons.** Telerik renders a Submit button unless `<FormButtons>` is given; ERP gives an empty one in 50
    forms to suppress it. NxForm renders buttons only from `Buttons`. ERP's hidden disabled submit button ("prevent
    implicit submission", ~18 forms) is `SubmitOnEnter="false"`.
94. **`Validation` replaces the default validator**: DataAnnotations unless `Validation` is set (ERP: FluentValidation in 105
    forms). An empty `<Validation></Validation>` means none.
95. **`form.EditContext` and `IsValid()` kept** (ERP reads `form1.EditContext` ×21 and calls `form1.IsValid()` ×79). The
    `EditContext` parameter reads back the context made for `Model`, as EditForm's does (BL0007 suppressed for that).
96. **Not carried over**: `Size` (×2), `Width` (×1, use `Style`), `ValidationMessageType` (×1), `ButtonsLayout` (×1),
    FormItem `Hint`. `Enabled` (×24) is `Disabled`; `LabelText` is `Label`, as on every Nx input.
97. **Grids fold to one column under 540 px of form width** (a container query), so a narrow window on a desktop folds
    too. Spans are clamped to the form's or group's `Columns`.
98. **One structural rule in nx-theme.css**: `form.nx-form { width: 100% }`. EditForm renders the `<form>`, so NxForm's
    scoped CSS cannot style it, and without a definite width a flex-column parent sized the form at its folded height
    (empty space under it). The alternative is a wrapper element around the form.
99. **NxValidationMessage is not generic**: `For` is `Expression<Func<object?>>`, so ERP's
    `For="@(() => x.Y)"` (515 uses) moves over unchanged. Messages sit in a polite live region.

100. **Icons are Lucide, not Telerik's.** Telerik's SVG paths are Progress's; the set is Lucide 0.460.0 (ISC, notice in
     `NxIcons.cs`), stroked rather than filled, under the exact Telerik names ERP uses: every `SvgIcon.*` name in
     ERP's .razor and .cs files has a member (131 in all), e.g. `SvgIcon.Trash` → `NxIcons.Trash`. Some are near
     matches: FilePdf/FileWord/FileTxt are generic file icons, Delicious (a brand logo) is a 2×2 grid, CaretAlt* are
     chevrons, CheckboxNull and ListRomanLower reuse Checkbox and ListOrdered. ThemeColor (×11) is `Color` (Primary,
     Success, Warning, Danger, Muted); `--nx-success` and `--nx-warning` are new tokens.
101. **Icon sizes** are seven steps, 12–48 px, `NxIconSize.XSmall … XXXLarge`.

102. **NxLoaderContainer defaults to ERP's usage**: a large spinner and no text (ERP passes a Template with exactly that
     in ~150 of 156). It covers its nearest positioned ancestor (`position: absolute; inset: 0`), as Telerik's does.
     ERP's `dark-overlay`/`medium-dark-overlay` classes become `--nx-loader-overlay-bg` values. The overlay blocks
     clicks but not Tab into what is under it.
103. **Loader types** keep Telerik's three (InfiniteSpinner → Spinner, ConvergingSpinner → Converging, Pulsing); sizes
     reuse `NxSize`.

104. **NxDateTimePicker supports DateTime and DateTime? only** (all ERP uses). Picking a day keeps the time of day and
     moves to the time list; picking a time closes. Telerik's per-part `DateTimePickerSteps` (Minute=15 in ERP) is
     `TimeStep` in minutes; seconds in a format are kept when typed, zero when picked.
105. **Masks only for all-number 24-hour formats** (`MM/dd/yyyy HH:mm`). ERP's formats with `h … tt`, `'at'` or seconds
     are typed freely and read leniently (`3/5 2pm`). DateText was not changed for seconds.
106. **The time list's CSS is a copy of NxTimePicker's** (same class names, so nx-picker.js scrolls it). Extracting a
     shared time-list component would touch NxTimePicker; not done.

107. **NxAutoComplete commits like the other Nx inputs**: on a pick, Enter or leaving, not every keystroke (Telerik's
     ValueChanged fires per keystroke). ERP's `OnChange` (×5) is `@bind-Value:after`; `OnBlur` (×3) was not added.
108. **NxAutoComplete has no phone sheet**: the suggestions open under the field at every width, since the field is
     already a text box (the combo's full-screen search would be a second one). `MobileMode` is fixed to Never.
109. **Default filter is Contains** (Telerik's is StartsWith; ERP passes Contains 9×, StartsWith 4×). `HeaderTemplate`
     (×1, "Similar customer names") was not added.

110. **Fixed a combo-engine bug (shared code)** found by the form's enum editors: with value-type items (an enum list),
     the unset `SelectedItem` parameter is the type's default, not null, and was selected over `Value`. NxComboBox,
     NxDropDown and NxMultiSelect with enum or int items showed the first item. One line in
     `NxComboBox.Selection.cs`, with a bUnit regression test.
111. **New nav categories "Forms" (NxForm) and "Display" (NxIcon, NxLoader)**; NxAutoComplete sits after NxMultiSelect,
     NxDateTimePicker after NxTimePicker.
112. **NxNumericTextBox swaps display text for edit text in the browser on focus** (nx-input.js `editOnFocus`), not
     from .NET a round trip later, which landed on text typed in between (the pass-5 Playwright flake). Side effect:
     a focus that is not a click (Tab, `FocusAsync`) now selects the edit text, as a Tab into any text field does, so
     typing replaces the number; a click still puts the caret. NxDatePicker and NxDateTimePicker swap the same way
     from .NET and have the same race; not changed.

## Pass 6 — from ERP's usages (read only)

113. **Answered (Brad): keep NxButtonGroup actions-only, toggles through NxToggleGroup.** **NxButtonGroup is for actions only.** ERP's 12 groups mix action buttons (Today ‹ ›) with toggle buttons whose
     `Selected` flags the page keeps exclusive by hand (This week / Next 2 weeks, Day / Week / Month, Table / List).
     Those toggles are one value picked from a set, which NxToggleGroup already does (single or `Multiple`, the latter
     for QualityControlList2's panes), so the group has no toggle buttons of its own. Say if you want an `NxButton`
     with `Selected` (`aria-pressed`) instead.
114. **Group defaults reach NxButton only.** NxButton now reads a cascaded NxButtonGroup for `Variant`, `Size` and
     `Disabled` (shared component change; a button that passes its own Variant or Size keeps it, even the default value).
     NxDropDownButton and NxSplitButton pass their own Variant and Size to their inner button, so a group's Variant and
     Size do not reach them; its Disabled does.
115. **ERP's per-button ThemeColor/FillMode** (Secondary + Outline on nearly every grouped button) is NxButton's default
     Secondary look; no outline variant was added.

116. **NxColorPicker is palette-only**, as ERP uses it (7 of 7 are palette views). Telerik's gradient view and typed
     hex/RGB input were not added. ERP's `NepColorPalettes.OfficeAndStandard` has 90 colors: the 70 Office ones are
     `NxColorPalettes.Office` (the default); ERP's 20 extra colors stay in ERP and are passed as `Colors`.
117. **The value is the swatch's string as given** (`#4371c4`); an incoming hex in other case or length, or `rgb(…)`,
     still marks its swatch. Telerik's `ValueFormat` (Hex ×1, the rest default) is not a parameter. ERP data saved in
     Telerik's default format may be `rgb(…)` strings: they display and match, and a new pick writes hex.
118. **The popup opens at every width** (no phone sheet): 10 columns of 30 px touch targets fit a 390 px screen.

119. **NxProgressBar**: ERP's label is always centered, so there is no `LabelPosition`. Telerik's `ProgressBarLabel
     Visible="false"` with a thin CSS class is `Thin`. `Indeterminate` was added (cheap, common need); no
     chunk/step bar.

120. **NxNotification keeps ERP's call shape** (`@ref` + `Show(new …Model { Text, ThemeColor, Closable, CloseAfter })`):
     `ThemeColor` is `Type`; Telerik's `Inverse` is `Neutral`. One animation (slide in from the side, fade when
     centered) replaces `AnimationType`. Defaults: bottom right (ERP 3 of 4), close after 5 s, closable.
121. **The countdown restarts** when the pointer or focus leaves a message, rather than resuming (WCAG 2.2.1 "pause").
122. **New categories reused**: NxButtonGroup under Buttons, NxColorPicker under Inputs, NxNotification under Menus &
     Overlays, NxProgressBar under Display.


## Pass 7 — from ERP's usages (read only)

123. **NxDialogService covers INepDialogFactory**, so ERP can swap the wrapper's inside and keep its ~540 callers:
     `AlertAsync`, `ConfirmAsync` (+ `confirmText`/`cancelText` for `ConfirmYesNoAsync`), `PromptAsync`,
     `ShowAsync(NxDialogOptions)` for `ShowAsync(NepDialogOptions)`, and `CreateProgress` (an `IProgress<string>` +
     `IDisposable`, as `IDialogProgress`). `ShowTernaryAsync` maps onto `ShowAsync`'s `bool?` (null = dismissed).
     The app adds `services.AddNxDialogs()` and one `<NxDialogHost />` in the layout; `RegisterRootCapture` goes away.
124. **Messages are plain text, not markup.** ERP's factory renders `message` with `AddMarkupContent`, and callers put
     server error text in it — an XSS hole. Nx keeps line breaks (`\n`); the wrapper should turn ERP's `<br/>` into `\n`.
     A caller that really needs markup passes `Content` (any RenderFragment, so `(MarkupString)html` still works).
     Say if you'd rather have an opt-in `MessageIsMarkup`.
125. **Polled `IsConfirmed`/`IsCancelled` are replaced by `NxDialogReference.Close(bool?)`** (from `Open`, or cascaded
     into Content). The wrapper can keep ERP's Func properties by polling them and calling `Close`.
126. **Not carried from NepDialogOptions**: `ConfirmButtonClass` (→ `ConfirmVariant`), `TitleTestId` (the title is
     `{Id}-title`), `BottomRow` (→ `Footer`), `FillWindow` (→ `FillContent`). `Validate` returns the message to show,
     as ERP's does; the order is ValidateAsync → Validate → OnConfirmAsync, with the confirm button busy meanwhile.
127. **NxWindow changed (shared)**: new `FullScreenOnPhone` (default true, as before) so a dialog stays a card on a
     phone; and nx-window.js now focuses a body `[autofocus]`, then a footer `[autofocus]` (the OK button of an alert),
     then the body's first focusable. `Closable` stays true by default, since Escape needs it.
128. **NxToggleButton** wraps NxButton (`aria-pressed`, `@bind-Selected`); its pressed look is new CSS in
     NxButton.razor.css (shared). In an NxButtonGroup it takes the group's Variant and Size. NxToggleGroup stays the
     choice for one-of-many (answer to #113).
129. **NxMediaQuery is `@bind-Matches`** instead of Telerik's `OnChange` (ERP always stores the bool). It reports once
     the browser answers, so it is `false` during prerender; ERP's pages already start from false.
130. **NxUpload posts from the browser (XHR)**, one request per file, field `files` (Telerik's default), cookies sent:
     ERP's `api/v1.0/Portal/Shared/File/Save` keeps working and its response body (the temp id) is
     `File.ResponseText` in `OnSuccess`. No `RemoveUrl`: ERP deletes temp files in `OnRemove` itself. Paste is a
     button (clipboard permission) and Ctrl+V on the focused zone. The demo's `/demo/upload` endpoint fails names
     containing "fail".
131. **NxFileSelect is Blazor's InputFile** underneath: files are streamed to .NET on demand via
     `OpenReadStream`, so the 1 ERP use (a demo page) works without a save URL.
132. **NxTreeView builds from flat Id/ParentId** (ERP's Permissions shape) or from `Children`. `HasChildren` and load
     on demand were not added (ERP loads the whole tree). Drag and drop is mouse only (HTML5 drag events do not fire
     on touch); the before/inside/after split assumes the 32 px desktop row.
133. **NxMaskedTextBox's value**: without `IncludeLiterals`, only the typed characters; with it, the text up to the
     last typed character (BidDialog's `26-04` while incomplete); nothing typed is `null`. Rules are Telerik's
     (`0 9 # L ? A a & C`, `\` escape). Typing is handled in nx-input.js (`beforeinput`), so fast typing never races.
134. **Categories**: NxToggleButton under Buttons; NxMaskedTextBox, NxFileSelect, NxUpload under Inputs; NxTreeView
     under Selection; NxDialog and NxMediaQuery under Layout.

## Pass 8 — from ERP's usages (read only)

135. **NxTileLayout's `ColumnWidth` is a maximum**, not a fixed width: columns share the row up to it. ERP passes
     300, 700 and 5000 px with `Columns="5"` and expects the dashboard to fill the page; a fixed width would scroll
     sideways. Tiles flow in reading order (not "dense"), a drop takes the target's place and the rest shift, and under
     600 px of its own width the layout stacks in one column. NepCardGrid keeps its own column breakpoints. Ctrl+arrows
     move and Shift+arrows resize from a tile's header (Telerik has no keys for it); row resizing needs a `RowHeight`.
136. **`GetState()` keeps Telerik's shape** (`ItemStates` with `Id`, `Order`, `ColSpan`, `RowSpan`, `Visible`), so
     NepCardGrid's `ReadState` ports as is. `SetState` before the tiles render still applies.
137. **NxCarousel**: `Page` counts from 1, as Telerik's does. A pause button shows with the dots (WCAG 2.2.2); ERP's TV
     screens hide arrows and dots, so they get none. After the last slide it jumps to the first instead of sliding
     back across all of them. Dots are 32 px touch targets (arrows 44 px): ten 44 px dots would not fit a phone.
138. **NxColorGradient is inline only**; put it in a dialog or popup. `Format` is which inputs show first, `ValueFormat`
     the value's form (ERP: hex both). `ShowOpacityEditor` defaults to true as in Telerik; ERP passes false. The hue is
     kept when a color goes grey, so dragging back out returns to it. **Shared change:** NxColorPicker's color parsing
     moved into `ColorMath` (internal), which both use; NxColorPicker behaves the same and its tests pass.
139. **NxSlider is a native range input.** ERP's one use is an int from 0 to 10 with nothing else set. Telerik shows
     buttons and ticks by default; Nx shows neither unless `ShowButtons` / `LargeStep` are set. No vertical slider and
     no range (two-thumb) slider: ERP uses neither.
140. **NxChipList removes, it does not select.** ERP's one use removes delivery tickets; Telerik's `SelectionMode` was not
     added. Say if you want it. The app removes the item from `Data` in `OnRemove`.
141. **Answered (Brad): NxButtonVariant gained Success, Warning and Info** (ERP: Success 35, Warning 11, Info 10 uses), with
     tokens in nx-theme.css (Warning is filled amber with dark text for contrast). The toolbar demo uses Success for Finalize
     and Info for Export PDF, as ERP does. NxToolBarButton takes `Text` (also its More menu entry); ERP's
     `<ToolBarButton>New</ToolBarButton>` becomes `Text="New"`.
142. **NxDateInput reuses the date picker's pieces**: `DateFormatter` (one new internal `Order` property) and
     nx-picker.js (new `stepDate`). Up/Down wrap within the part (December → January keeps the year) and an empty
     field starts from today.
143. **Categories**: NxToolBar under Buttons; NxSlider and NxColorGradient under Inputs; NxDateInput under Dates;
     NxTileLayout under Layout; NxChipList and NxCarousel under Display.

## Pass 9 — NxChart, from ERP's 10 chart pages (read only)

144. **The chart is drawn by .NET as SVG**; nx-chart.js only measures the plot (ResizeObserver), finds the point under the
     pointer and keeps the arrow keys from scrolling. Each hover change re-renders the surface; Blazor's diff only touches
     what changed. Sized for ERP's data (up to a year of days times a few series); thousands of points are not tuned.
145. **The API keeps Telerik's field-name binding** (`Data` + `Field` / `CategoryField` / `ColorField` with `nameof`), with
     series and axes as child components, so ERP's pages port nearly line for line. Renamed: `ChartSeriesStack Enabled`
     → `Stacked`, `ChartSeriesMarkers Visible` → `Markers`, `ChartSeriesLine Color/Width` → `LineColor` / `LineWidth`,
     `ChartSeriesLabels` → `ShowLabels` / `LabelFormat`, `ChartSeriesTooltip Template` → `TooltipTemplate` (context
     `NxChartPoint`: `DataItem`, `FormattedValue`, `Percentage`…), `ChartLegend Position` → `LegendPosition`,
     `ChartTitle` → `Title`, axis label `Format` → `LabelFormat` (both `{0:C0}` and `C0` work).
146. **One category axis.** The storage history's two axes (a hidden one holding the dates, a visible one with sparse
     labels) become one axis with `Categories` and `Labels`. The engagement chart's `AxisCrossingValue` is not needed: the
     second value axis draws on the right on its own; a third, invisible axis still scales its series.
147. **Defaults**: legend at the bottom (Telerik's is right) and, when asked for on the right, under the chart below 360 px
     wide; height 400 px; date categories every step of the base unit, empty ones included; aggregate Max (Telerik's
     default; BatchSummary passes Sum); line points in the middle of their category, as Telerik draws them.
148. **`Refresh()` is `RefreshAsync()`**: ERP calls it after a tab or tile shows; the ResizeObserver covers most of that.
149. **Not built** (ERP uses none): a shared tooltip for all series, zoom and pan, donut, scatter, export to an image,
     entry animations, labels on line points.
150. **The pointer picks the nearest point in the category under it**; a drawn marker wins a near-tie over a series drawn
     without markers, so the control chart's limit lines never steal a click from the tests beside them.
151. **Palette**: `--nx-chart-1` … `--nx-chart-8` (blue, orange, green, red, purple, teal, magenta, brown), light and dark;
     NxChart sits under Display.

## NxChart status (paused 10/26)

- **Works** (seen in the browser at 390/768/1280, bUnit 25/25): line, area, column, bar and pie; date axes (hours to years,
  aggregates) and plain categories with `Labels`; two value axes; stacking; dashes, markers, value labels; legend that
  hides series or slices; tooltips by hover, tap and arrow keys; `OnSeriesClick`; redraw on resize; a screen-reader table.
  Four demo pages, a home card and the `chart` slug in SiteLayoutTests are in.
- **Missing**: `ChartTests` (Playwright, 9 tests) is written but has never been run; dark theme and the home card not
  looked at; grid lines of a second value axis do not line up with the first; large data (thousands of points) not
  measured; decisions #144–151 not reviewed.
- **Next**: run the Playwright fixture, review #144–151, then port one ERP page (ControlCharts or StorageHistory) as a trial.
