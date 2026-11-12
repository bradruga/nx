namespace Nx.Components;

/// <summary>
/// One entry in <see cref="NxEditor.Tools"/>: a built-in tool, a <see cref="Separator"/> between groups, or a
/// <see cref="Custom"/> slot filled by an <see cref="NxEditorCustomTool"/> of the same name.
/// </summary>
public sealed class NxEditorTool
{
    private NxEditorTool(string name, string title, NxEditorToolKind kind)
    {
        Name = name;
        Title = title;
        Kind = kind;
    }

    /// <summary>The tool's command name (<c>bold</c>, <c>insertTable</c>…), or a custom tool's name.</summary>
    public string Name { get; }

    /// <summary>The tooltip and accessible name.</summary>
    public string Title { get; }

    internal NxEditorToolKind Kind { get; }

    internal bool IsTableEdit => Kind == NxEditorToolKind.Button && TableEdits.Contains(Name);

    /// <summary>A custom tool's slot: an <see cref="NxEditorCustomTool"/> with this <paramref name="name"/> renders here.</summary>
    public static NxEditorTool Custom(string name) => new(name, name, NxEditorToolKind.Custom);

    private static NxEditorTool Button(string name, string title) => new(name, title, NxEditorToolKind.Button);

    /// <summary>A line between groups of tools.</summary>
    public static NxEditorTool Separator { get; } = new("separator", "", NxEditorToolKind.Separator);

    /// <summary>Undoes the last change (<kbd>Ctrl+Z</kbd>).</summary>
    public static NxEditorTool Undo { get; } = Button("undo", "Undo");

    /// <summary>Redoes what was undone (<kbd>Ctrl+Y</kbd>).</summary>
    public static NxEditorTool Redo { get; } = Button("redo", "Redo");

    /// <summary>Bold (<kbd>Ctrl+B</kbd>).</summary>
    public static NxEditorTool Bold { get; } = Button("bold", "Bold");

    /// <summary>Italic (<kbd>Ctrl+I</kbd>).</summary>
    public static NxEditorTool Italic { get; } = Button("italic", "Italic");

    /// <summary>Underline (<kbd>Ctrl+U</kbd>).</summary>
    public static NxEditorTool Underline { get; } = Button("underline", "Underline");

    /// <summary>Strikethrough.</summary>
    public static NxEditorTool Strikethrough { get; } = Button("strikethrough", "Strikethrough");

    /// <summary>A list of block formats: paragraph, headings 1–6, quotation, preformatted.</summary>
    public static NxEditorTool Format { get; } = new("format", "Format", NxEditorToolKind.Format);

    /// <summary>A list of font sizes.</summary>
    public static NxEditorTool FontSize { get; } = new("fontSize", "Font size", NxEditorToolKind.FontSize);

    /// <summary>A list of font families.</summary>
    public static NxEditorTool FontFamily { get; } = new("fontFamily", "Font", NxEditorToolKind.FontFamily);

    /// <summary>The text color, from <see cref="NxEditor.Colors"/>.</summary>
    public static NxEditorTool ForeColor { get; } = new("foreColor", "Text color", NxEditorToolKind.ForeColor);

    /// <summary>The highlight (background) color, from <see cref="NxEditor.Colors"/>, or none.</summary>
    public static NxEditorTool BackgroundColor { get; } = new("backColor", "Highlight color", NxEditorToolKind.BackgroundColor);

    /// <summary>Aligns the paragraph left.</summary>
    public static NxEditorTool AlignLeft { get; } = Button("alignLeft", "Align left");

    /// <summary>Centers the paragraph.</summary>
    public static NxEditorTool AlignCenter { get; } = Button("alignCenter", "Center");

    /// <summary>Aligns the paragraph right.</summary>
    public static NxEditorTool AlignRight { get; } = Button("alignRight", "Align right");

    /// <summary>Justifies the paragraph.</summary>
    public static NxEditorTool AlignJustify { get; } = Button("alignJustify", "Justify");

    /// <summary>A bulleted list.</summary>
    public static NxEditorTool UnorderedList { get; } = Button("unorderedList", "Bulleted list");

    /// <summary>A numbered list.</summary>
    public static NxEditorTool OrderedList { get; } = Button("orderedList", "Numbered list");

    /// <summary>Indents the paragraph, or nests the list item.</summary>
    public static NxEditorTool Indent { get; } = Button("indent", "Indent");

    /// <summary>Outdents the paragraph or list item.</summary>
    public static NxEditorTool Outdent { get; } = Button("outdent", "Outdent");

    /// <summary>Inserts or edits a link (<kbd>Ctrl+K</kbd>).</summary>
    public static NxEditorTool CreateLink { get; } = Button("createLink", "Insert link");

    /// <summary>Removes the link at the caret.</summary>
    public static NxEditorTool Unlink { get; } = Button("unlink", "Remove link");

    /// <summary>Inserts a picture by its address.</summary>
    public static NxEditorTool InsertImage { get; } = Button("insertImage", "Insert image");

    /// <summary>Inserts a table, sized by dragging over a grid.</summary>
    public static NxEditorTool InsertTable { get; } = Button("insertTable", "Insert table");

    /// <summary>Adds a row above the caret's cell.</summary>
    public static NxEditorTool AddRowBefore { get; } = Button("addRowBefore", "Add row above");

    /// <summary>Adds a row below the caret's cell.</summary>
    public static NxEditorTool AddRowAfter { get; } = Button("addRowAfter", "Add row below");

    /// <summary>Adds a column left of the caret's cell.</summary>
    public static NxEditorTool AddColumnBefore { get; } = Button("addColumnBefore", "Add column left");

    /// <summary>Adds a column right of the caret's cell.</summary>
    public static NxEditorTool AddColumnAfter { get; } = Button("addColumnAfter", "Add column right");

    /// <summary>Deletes the caret's row.</summary>
    public static NxEditorTool DeleteRow { get; } = Button("deleteRow", "Delete row");

    /// <summary>Deletes the caret's column.</summary>
    public static NxEditorTool DeleteColumn { get; } = Button("deleteColumn", "Delete column");

    /// <summary>Deletes the caret's table.</summary>
    public static NxEditorTool DeleteTable { get; } = Button("deleteTable", "Delete table");

    /// <summary>Merges the selected cells into one.</summary>
    public static NxEditorTool MergeCells { get; } = Button("mergeCells", "Merge cells");

    /// <summary>Splits a merged cell back into single cells.</summary>
    public static NxEditorTool SplitCell { get; } = Button("splitCell", "Split cell");

    private static readonly HashSet<string> TableEdits =
        ["addRowBefore", "addRowAfter", "addColumnBefore", "addColumnAfter", "deleteRow", "deleteColumn", "deleteTable", "mergeCells", "splitCell"];

    /// <summary>The tools shown when <see cref="NxEditor.Tools"/> is not set.</summary>
    public static IReadOnlyList<NxEditorTool> Default { get; } =
    [
        Undo, Redo, Separator,
        Bold, Italic, Underline, Separator,
        Format, Separator,
        UnorderedList, OrderedList, Indent, Outdent, Separator,
        AlignLeft, AlignCenter, AlignRight, Separator,
        CreateLink, Unlink
    ];

    /// <summary>Every built-in tool, in groups.</summary>
    public static IReadOnlyList<NxEditorTool> All { get; } =
    [
        Undo, Redo, Separator,
        Bold, Italic, Underline, Strikethrough, Separator,
        Format, FontFamily, FontSize, ForeColor, BackgroundColor, Separator,
        AlignLeft, AlignCenter, AlignRight, AlignJustify, Separator,
        UnorderedList, OrderedList, Indent, Outdent, Separator,
        CreateLink, Unlink, InsertImage, Separator,
        InsertTable, AddRowBefore, AddRowAfter, AddColumnBefore, AddColumnAfter, DeleteRow, DeleteColumn, DeleteTable, MergeCells, SplitCell
    ];

    /// <inheritdoc />
    public override string ToString() => Name;
}

internal enum NxEditorToolKind
{
    Button,
    Separator,
    Custom,
    Format,
    FontSize,
    FontFamily,
    ForeColor,
    BackgroundColor
}
