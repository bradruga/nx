namespace Nx.Components;

// NxEditor's toolbar icons. Text and alignment icons are Lucide 0.460.0 (ISC, notice in NxIcons.cs); the table edits are
// drawn here in the same 24×24 stroked style. Undo, redo, lists, link, image and table come from NxIcons.
internal static class EditorIcons
{
    public static readonly NxIconData Bold = new("bold", """<path d="M6 12h9a4 4 0 0 1 0 8H7a1 1 0 0 1-1-1V5a1 1 0 0 1 1-1h7a4 4 0 0 1 0 8"/>""");
    public static readonly NxIconData Italic = new("italic", """<line x1="19" x2="10" y1="4" y2="4"/> <line x1="14" x2="5" y1="20" y2="20"/> <line x1="15" x2="9" y1="4" y2="20"/>""");
    public static readonly NxIconData Underline = new("underline", """<path d="M6 4v6a6 6 0 0 0 12 0V4"/> <line x1="4" x2="20" y1="20" y2="20"/>""");
    public static readonly NxIconData Strikethrough = new("strikethrough", """<path d="M16 4H9a3 3 0 0 0-2.83 4"/> <path d="M14 12a4 4 0 0 1 0 8H6"/> <line x1="4" x2="20" y1="12" y2="12"/>""");
    public static readonly NxIconData AlignLeft = new("align-left", """<line x1="21" x2="3" y1="6" y2="6"/> <line x1="15" x2="3" y1="12" y2="12"/> <line x1="17" x2="3" y1="18" y2="18"/>""");
    public static readonly NxIconData AlignCenter = new("align-center", """<line x1="21" x2="3" y1="6" y2="6"/> <line x1="17" x2="7" y1="12" y2="12"/> <line x1="19" x2="5" y1="18" y2="18"/>""");
    public static readonly NxIconData AlignRight = new("align-right", """<line x1="21" x2="3" y1="6" y2="6"/> <line x1="21" x2="9" y1="12" y2="12"/> <line x1="21" x2="7" y1="18" y2="18"/>""");
    public static readonly NxIconData AlignJustify = new("align-justify", """<line x1="3" x2="21" y1="6" y2="6"/> <line x1="3" x2="21" y1="12" y2="12"/> <line x1="3" x2="21" y1="18" y2="18"/>""");
    public static readonly NxIconData Indent = new("indent", """<polyline points="3 8 7 12 3 16"/> <line x1="21" x2="11" y1="12" y2="12"/> <line x1="21" x2="11" y1="6" y2="6"/> <line x1="21" x2="11" y1="18" y2="18"/>""");
    public static readonly NxIconData Outdent = new("outdent", """<polyline points="7 8 3 12 7 16"/> <line x1="21" x2="11" y1="12" y2="12"/> <line x1="21" x2="11" y1="6" y2="6"/> <line x1="21" x2="11" y1="18" y2="18"/>""");
    public static readonly NxIconData Unlink = new("unlink", """<path d="M9 17H7A5 5 0 0 1 7 7"/> <path d="M15 7h2a5 5 0 0 1 4 8"/> <line x1="8" x2="12" y1="12" y2="12"/> <line x1="2" x2="22" y1="2" y2="22"/>""");
    public static readonly NxIconData TextColor = new("baseline", """<path d="M4 20h16"/> <path d="m6 16 6-12 6 12"/> <path d="M8 12h8"/>""");
    public static readonly NxIconData Highlight = new("highlighter", """<path d="m9 11-6 6v3h9l3-3"/> <path d="m22 12-4.6 4.6a2 2 0 0 1-2.8 0l-5.2-5.2a2 2 0 0 1 0-2.8L14 4"/>""");
    public static readonly NxIconData AddRowBefore = new("row-before", """<rect x="3" y="11" width="18" height="10" rx="2"/> <path d="M3 16h18"/> <path d="M12 2v6"/> <path d="M9 5h6"/>""");
    public static readonly NxIconData AddRowAfter = new("row-after", """<rect x="3" y="3" width="18" height="10" rx="2"/> <path d="M3 8h18"/> <path d="M12 16v6"/> <path d="M9 19h6"/>""");
    public static readonly NxIconData AddColumnBefore = new("column-before", """<rect x="11" y="3" width="10" height="18" rx="2"/> <path d="M16 3v18"/> <path d="M2 12h6"/> <path d="M5 9v6"/>""");
    public static readonly NxIconData AddColumnAfter = new("column-after", """<rect x="3" y="3" width="10" height="18" rx="2"/> <path d="M8 3v18"/> <path d="M16 12h6"/> <path d="M19 9v6"/>""");
    public static readonly NxIconData DeleteRow = new("row-delete", """<rect x="3" y="3" width="18" height="10" rx="2"/> <path d="M3 8h18"/> <path d="m9 16 6 6"/> <path d="m15 16-6 6"/>""");
    public static readonly NxIconData DeleteColumn = new("column-delete", """<rect x="3" y="3" width="10" height="18" rx="2"/> <path d="M8 3v18"/> <path d="m16 9 6 6"/> <path d="m22 9-6 6"/>""");
    public static readonly NxIconData DeleteTable = new("table-delete", """<path d="M12 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2v7"/> <path d="M3 9h18"/> <path d="M9 3v18"/> <path d="m16 16 5 5"/> <path d="m21 16-5 5"/>""");
    public static readonly NxIconData MergeCells = new("merge-cells", """<rect x="3" y="3" width="18" height="18" rx="2"/> <path d="M3 12h4"/> <path d="M17 12h4"/> <path d="m10 9 3 3-3 3"/> <path d="m14 9-3 3 3 3"/>""");
    public static readonly NxIconData SplitCell = new("split-cell", """<rect x="3" y="3" width="18" height="18" rx="2"/> <path d="M12 3v5"/> <path d="M12 16v5"/> <path d="m8 9-3 3 3 3"/> <path d="m16 9 3 3-3 3"/>""");

    public static NxIconData? For(string tool) => tool switch
    {
        "undo" => NxIcons.Undo,
        "redo" => NxIcons.Redo,
        "bold" => Bold,
        "italic" => Italic,
        "underline" => Underline,
        "strikethrough" => Strikethrough,
        "alignLeft" => AlignLeft,
        "alignCenter" => AlignCenter,
        "alignRight" => AlignRight,
        "alignJustify" => AlignJustify,
        "unorderedList" => NxIcons.ListUnordered,
        "orderedList" => NxIcons.ListOrdered,
        "indent" => Indent,
        "outdent" => Outdent,
        "createLink" => NxIcons.Link,
        "unlink" => Unlink,
        "insertImage" => NxIcons.Image,
        "insertTable" => NxIcons.Table,
        "addRowBefore" => AddRowBefore,
        "addRowAfter" => AddRowAfter,
        "addColumnBefore" => AddColumnBefore,
        "addColumnAfter" => AddColumnAfter,
        "deleteRow" => DeleteRow,
        "deleteColumn" => DeleteColumn,
        "deleteTable" => DeleteTable,
        "mergeCells" => MergeCells,
        "splitCell" => SplitCell,
        _ => null
    };
}
