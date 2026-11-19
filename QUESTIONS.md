# Open Questions

Decisions I made without you. Each one says what I picked and why, so you can answer by number.
Answered entries are removed; numbers are kept so references stay valid. Entries #24–#176 were accepted as built on
11/19 and stay as the record of why; the 11/19 decisions (lambdas everywhere, one size and one mobile-mode enum, combo
virtualization by threshold, `ItemByValue`, opt-in phone sheet, `NxWindowHost`, window move and resize events,
`DateTimeOffset`, hour and minute columns, record radio groups, container theming, Escape that only clears) are built.

## Open after the 11/19 build

181. **Radio groups with hand-written buttons name both types** (`TItem="X" TValue="X"`): Razor cannot infer `TItem`
     without `Data`. The alternative is a separately named component for record lists. Rec: keep.
185. **A hosted window loses the page's cascading values and inherited CSS** (a theme class on a panel, `::deep` rules).
     Such windows need `Inline="true"`; the search dialog and the theme builder's dialog did. NxDialogService's dialogs
     stay in NxDialogHost and take its tokens, not the caller's.
186. **Gantt**: `OnUpdate` carries the new `Start` / `End` and the page writes them (no `Apply()`); a cell-edit update no
     longer names its column; dates are `DateTime` only (convert `DateOnly` / `DateTimeOffset` in the lambda);
     `Dependencies` is a list of the new `NxGanttDependency`; a column's `Title` no longer defaults to a field name.
187. **Time columns list minutes every 5** (every `Step` when under 5); other minutes are typed. All 60 would mean a
     long scroll. `NxTimeColumns` is public, with no demo pages of its own.
188. **A kept `DateTimeOffset` offset can go stale across daylight saving**: a January value at -06:00 moved to July
     stays -06:00, by the keep-the-previous-offset rule.
189. **Combo lists of 200+ rows pin every row to one height**, group headers and template rows included; with a
     `DataProvider` the list is hidden for one round trip the first time it is measured. `ItemByValue` is ignored with
     `Data`.
190. **Three colors do not follow a container's accent** (literals, not derived): `--nx-accent-subtle`,
     `--nx-scheduler-today-bg`, `--nx-chart-1`. A container sets them itself.
191. **Escape pressed within one round trip of the first character** still closes the window: the clear button that
     marks the key used has not rendered yet.
194. **A component removed while its start-up script calls are still in flight can crash the circuit** (the whole page
     stops responding). Reproduced by switching the picker playground's type and typing at once: `watchViewport` got a
     missing element, and a disposed module was called from `OnAfterRenderAsync`. Fixed in nx-popup.js, nx-picker.js,
     nx-colorpicker.js and the four pickers. Other components with several awaited calls in `OnAfterRenderAsync` have
     the same shape. Rec: sweep them all (wrap the start-up calls as the pickers now do).
195. **Keys pressed in the round trip between a popup showing and its script going live are lost** (editor table
     picker; the color picker's Escape was fixed). Files picked before nx-upload.js loaded are now kept; NxFileSelect
     (Blazor's InputFile) can still miss them.
     Same window: on a phone, a tap on a time picker before `watchViewport` has answered opens the popup, not the
     sheet (`TimePickerTests.Phone_AutoModeUsesSheet`, 2 of 4 fixture runs). About one test per full Playwright run
     fails this way, a different one each time. Rec: one sweep for "input in the first round trips after a page or
     popup appears", rather than one test at a time.
193. **Seen once, not reproduced**: `NxFileManagerTests.Search_FindsBelowTheOpenFolder_WithLocation` (bUnit, stale
     handler id suspected); `AutoCompleteTests.Typing_Suggests_UnderTheField_NothingHighlighted` (popup above the
     field); `TimePickerTests.List_ActiveScrollsIntoView`; `SliderChipDateInputTests.Chips_ArrowsMove_DeleteRemoves_FocusStays`;
     `ComboBoxTests.Multiple_SpaceTypesWhileFiltering`; `MultiSelectTests.Toggle_ChipsAndKeepOpen`;
     `DatePickerTests.Validation_RequiredAndCrossField` (0 of 12 alone).
**Open doubts (no code change now):** the chart's second value axis does not line up with the first axis's grid lines
(aligning them means less round numbers); large charts and long editor documents are not measured. Next for the chart:
port one ERP page (ControlCharts or StorageHistory) as a trial.

## Pass 2 — demo site

24. **Drawer below 900 px.** Tablet portrait (768) gets the drawer; landscape tablet and up get the fixed sidebar.
    A narrow icon rail at 768 would need icons per component; I skipped it.
25. **API tables scroll inside their box on a phone**, at a readable 620 px minimum, rather than reflowing to
    label/value cards. Reflow would need a `data-label` on every cell of every table.
26. **Routes are `/{slug}/{page}`**, with slugs `button`, `textbox`, `numeric`, `combobox`, `checkbox`, `switch`, `radio`,
    `togglegroup`. The bare `/{slug}` still opens Overview.

## Pass 2 — NxComboBox

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
35. **NxNumericTextBox formats only while unfocused.** Focus shows the plain number. Format `"P"` shows the value ×100
    when not focused; use `Suffix="%"` with plain values instead.
36. **Numeric parsing drops the culture's group separators**, so in de-DE `1.5` reads as 15 — what Excel does. It also
    accepts currency symbols, `%` and accounting parentheses, and the no-break spaces .NET formats de-DE and fr-CA with.
38. **Numeric `inputmode`.** It is `numeric`/`decimal` only when Min ≥ 0, because the iOS number pad has no minus key.
39. **double/float beyond ±7.9e28** (decimal's range) are not supported. Fine for the ERP, I think.
40. **Check box and switch also toggle on Enter**, because they are native buttons. ARIA specifies Space only;
    blocking Enter needs JS.
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
51. **Range picking:** the first pick is the start, the second the end; a second pick before the start restarts there;
    a new start after the end clears the end; a typed start after the end swaps them. No hover preview of the band
    (it would cost a server round trip per mouse move).
52. **Range validation is per end**: `StartValueExpression` and `EndValueExpression` are separate fields, so a rule
    across both goes on the model (as in the demo).
54. **Time picker `DateTime` with no previous value uses today's date.**
55. **`NxCalendar` and `NxPickerSheet` are public** (Razor components cannot be internal). NxCalendar is documented on
    the NxDatePicker pages rather than having its own nav entry.
56. **New nav category "Dates"** between Inputs and Selection.

## Pass 3 — NxWindow

57. **`Modal` defaults to `true`.** Telerik's Window defaults to non-modal; ERP dialogs are mostly modal.
61. **Minimized windows stay where they were** (title bar only, 280 px wide); on phones they dock to the bottom.
    No taskbar of minimized windows.
62. **Size presets are widths only** (Small 400, Medium 600, Large 900, Auto); height follows the content up to the
    viewport unless `Height` is set.
63. **New nav category "Layout"** for NxWindow, NxTabStrip and NxSplitter.

## Pass 3 — NxTabStrip

64. **`ActiveTab` is a string key**: the tab's `Id`, else its `Title`. No `ActiveIndex` — indexes shift when tabs close.
    With no match the first enabled tab shows, without raising `ActiveTabChanged`.
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
76. **NxDropDown's DataProvider is NxComboBox's signature** (`NxComboBoxRequest`, filter always empty), per "same
    DataProvider", and it is called each time the list opens.
77. **NxDropDown type-ahead with the list closed selects** the next match (a native select does this). Type-ahead
    prefixes reset after 0.7 s or on any non-letter key.
78. **NxMultiSelect "filter box" is the field itself** (type to filter), as in NxComboBox `Multiple`; `Filterable="false"`
    makes it pick-only with type-ahead. A separate search box inside the popup was not added.
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

90. **An item's messages come with it.** NxFormItem shows its Field's messages under the editor, also around your own
    editor. ERP's templates carry their own `TelerikValidationMessage`; migration drops those (or the item would show two).
91. **Generated editors** (ERP has 194 `<FormItem Field=… />` with no template): string → NxTextBox, numbers →
    NxNumericTextBox, bool → NxCheckBox (labelled after the box), DateTime/DateOnly → NxDatePicker (a DateTime keeps its
    time; ERP never used Telerik's DateTimePicker editor there), TimeOnly/TimeSpan → NxTimePicker, enum → NxDropDown with
    the names in words. Other types throw and ask for an editor inside the item. `EditorType` (Switch ×5, TextArea ×3)
    was not added: put an NxToggleSwitch or NxTextArea inside the item.
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

111. **New nav categories "Forms" (NxForm) and "Display" (NxIcon, NxLoader)**; NxAutoComplete sits after NxMultiSelect,
     NxDateTimePicker after NxTimePicker.

## Pass 6 — from ERP's usages (read only)

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
142. **NxDateInput reuses the date picker's pieces**: `DateFormatter` (one new internal `Order` property) and
     nx-picker.js (new `stepDate`). Up/Down wrap within the part (December → January keeps the year) and an empty
     field starts from today.
143. **Categories**: NxToolBar under Buttons; NxSlider and NxColorGradient under Inputs; NxDateInput under Dates;
     NxTileLayout under Layout; NxChipList and NxCarousel under Display.

## Pass 10 — NxEditor and NxChat, from ERP's usages (read only)

153. **Tools keep Telerik's names as `NxEditorTool` members in one list**, a `Separator` in place of `EditorButtonGroup`:
     `new EditorButtonGroup(new Bold(), new Italic())` → `NxEditorTool.Bold, NxEditorTool.Italic, NxEditorTool.Separator`.
     `new CustomTool("Upload")` → `NxEditorTool.Custom("Upload")` with `<NxEditorCustomTool Name="Upload">` in `CustomTools`.
     Per-tool settings are gone: one `Colors` for text and highlight; titles are ERP's ("Text color", "Highlight color").
154. **Dropped Telerik parameters**: `EditMode` (always a div; ERP passes Div), `Adaptive` (the toolbar wraps), `Plugins`
     (ProseMirror), `EditorPasteSettings` (paste is always cleaned, as ERP's settings ask). `ExecuteAsync(new
     HtmlCommandArgs("insertHtml", html))` → `InsertHtmlAsync(html)`.
155. **Paste**: HTML is cleaned (scripts, comments, classes, Office styles, unsafe links and pictures; Word lists become
     lists); plain text pastes as the browser does; a page's own paste handler that calls `preventDefault` wins. NepEditor's
     picture-paste JS targets Telerik's DOM, so porting it means pointing it at `.nx-editor-content`.
156. **Value**: `""` when empty, so `[Required]` works; `ValueChanged` 100 ms after typing stops (Telerik's
     `DebounceDelay`), at once for a tool, undo or paste, and on leaving.
157. **Inserted tables carry inline borders** (`1px solid #c8c8cc`) so they show wherever the HTML is displayed (emails,
     reports); inside the editor, borderless tables get dashed guides.
158. **Not built** (ERP uses none): view HTML, subscript/superscript, clear formatting, picture resize handles, font and size
     lists as parameters, find and replace.
160. **Doubts**: in NxEditor the format/font/size lists and color pickers are their own tab stops (NxToolBar's roving tab
     stop covers its buttons only); removing a text color clears it inside the selection, not from a colored span around it;
     large documents are not measured.

## Pass 11 — NxPdfViewer, NxPopup, NxSignature, NxCircularGauge, from ERP's usages (read only)

162. **ERP never shows TelerikPdfViewer**: `NepPdfViewer` defaults to its own pdf.js iframe (with NEP's rectangle and
     snap-point drawing); 6 pages pass `Browser`, 1 `PdfJs`, none `Telerik`. So NxPdfViewer is new ground rather than a
     port: `Data` / `Url`, `Zoom`, `@bind-Page`, `Tools` flags (Pager, Zoom, Search, Download, Print), `OnLoad` / `OnError`.
     Moving NepPdfJsViewer's rectangles onto NxPdfViewer is a later step, if wanted.
163. **Print** opens the browser's own print of the file (a hidden frame), not pdf.js's page-by-page print rendering.
     **Not built**: thumbnails, outline, rotate, annotations editing, forms, password prompt (an encrypted PDF shows the
     error).
164. **NxPopup flattens Telerik's four align parameters** into `Placement` (side) + `Align` (start/center/end): ERP's one
     use (anchor right-bottom, popup right-top) is `Placement="Bottom" Align="End"`. Collision is always flip on the side,
     fit along it. `Show()` / `Hide()` → `@bind-Visible`; `Refresh()` → `RefreshAsync()` (rarely needed: it re-places after
     every render, scroll, resize and size change). Focus is not moved into it, as Telerik's.
165. **NxSignature** binds a PNG data URL as Telerik's (ERP stores `SignatureSource`), exported at `ExportScale` 2. Ink and
     paper are saved in the image, so the pad stays white in the dark theme. Not built: Telerik's maximize-to-dialog
     button and its `Smooth` option (strokes are always smoothed).
166. **NxCircularGauge flattens Telerik's scales/pointers** to `Value` / `Min` / `Max` / `Color` / `CenterTemplate`: ERP's one
     use has one scale and one pointer. Not built: ranges (colored bands), labels and ticks, several pointers.
## Pass 12 — NxTreeList, from ERP's yard page (read only)

168. **NxGrid cannot show parent/child rows**: it has one level of `GroupBy` and no tree, so the yard page could not move to
     it. NxTreeList is new, scoped to that page: flat data, two columns, single selection, two yards open at the start,
     `Height="100%"`. No sorting, filtering, paging, editing, resizing or reordering of columns.
169. **It is NxTreeView, not a copy**: NxTreeList renders NxTreeView (expand, select, keyboard, two-way bindings) with an
     `ItemTemplate` of cells, so NxTreeView did not change. It stays an ARIA `tree`, not a `treegrid`: each cell reads with
     its column title ("# Pieces: 18"); the visual header is hidden from screen readers.
## Pass 13 — NxScheduler, NxFileManager, NxGantt, from ERP's usages (read only)

172. **NxScheduler is what the shift schedule uses**: read-only (`AllowCreate/Update/Delete` were all false), one week
     view, fields by name with Telerik's defaults (`Start`, `End`, `Title`), `ItemTemplate`, `@bind-Date`. Telerik's
     `SchedulerWeekView` parameters moved onto the component; its `DateTime` times are `TimeOnly`
     (`StartTime="TimeOnly.FromDateTime(vm.SchedulerDayStart)"`). Overnight shifts split at midnight, as the page's
     previous-week spillover needs. Not built: day, month and timeline views, all-day row, creating, dragging or editing
     items, resources.
173. **NxFileManager builds in what the project files page worked around**: folders before files (the page reflected into
     Telerik's sort member), a read-only Download in place of Rename/Delete (hidden there with CSS), `OnOpen` for a file
     (Telerik has no open event; the page caught double-clicks on a wrapper and taps by pointer type), a search that
     matches anywhere below the open folder (the page's own tool), and a tree that stays open to the folder shown. Bound to
     the flat list with Telerik's field names; `Path` / `PathChanged` became `@bind-Folder` (the folder item). Not built:
     upload, rename, delete, move, new folder, multi-select, context menu.
174. **NxGantt covers the project schedule page except two things**: column resizing (`ColumnResizable`) and multi-column
     sort (`SortMode.Multiple`: one column at a time here). Telerik's popup edit form became `OnEdit`: the page opens its own
     `ProjectSchedulePopupForm` in an NxDialog, as it already supplies one. `GetState`/`SetState` became parameters and
     members: `@bind-TreeListWidth`, `@bind-ExpandedItems`, `HasSort` / `HasFilter`, `ClearSortAsync` /
     `ClearFilterAsync`, `OnStateChanged`. `OnCreate` / `OnDelete` stay the page's (its Add button and row menu).
     Dependencies are drawn, and created by dragging between bar handles (`OnDependencyCreate`).
175. **Cell editing commits on leaving**: a press outside the cell, Enter, Tab or Escape ends it and raises `OnUpdate`; the
     editor template has already written the item (as ERP's templates bind directly). There is no cancel that restores
     the old value.
