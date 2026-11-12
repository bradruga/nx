using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;

namespace Nx.Components.Tests;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class NxEditorTests : BunitContext
{
    private BunitJSModuleInterop module = default!;

    [SetUp]
    public void SetUp()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        module = JSInterop.SetupModule("./_content/Nx.Components/nx-editor.js");
        module.Mode = JSRuntimeMode.Loose;
        // nx-popup.js and nx-combobox.js for the format list, nx-window.js for the dialogs.
        foreach (var path in new[] { "nx-popup.js", "nx-combobox.js", "nx-window.js", "nx-toolbar.js", "nx-input.js" })
            JSInterop.SetupModule($"./_content/Nx.Components/{path}").Mode = JSRuntimeMode.Loose;
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
    }

    private IRenderedComponent<NxEditor> RenderBound(List<string?> raised, string? initial = null,
        Action<ComponentParameterCollectionBuilder<NxEditor>>? configure = null)
    {
        IRenderedComponent<NxEditor>? cut = null;
        cut = Render<NxEditor>(p =>
        {
            p.Add(x => x.Id, "ed");
            p.Add(x => x.Value, initial);
            p.Add(x => x.ValueChanged, (string? v) =>
            {
                raised.Add(v);
                cut?.Render(q => q.Add(x => x.Value, v));
            });
            configure?.Invoke(p);
        });
        return cut;
    }

    private static JsonElement State(object state) => JsonSerializer.SerializeToElement(state);

    private int Calls(string identifier) => module.Invocations.Count(i => i.Identifier == identifier);

    [Test]
    public void Render_TextboxAndDefaultToolbar()
    {
        var cut = Render<NxEditor>(p => p.Add(x => x.Id, "notes").Add(x => x.AriaLabel, "Notes").Add(x => x.Placeholder, "Write…"));

        var content = cut.Find("#notes");
        Assert.That(content.GetAttribute("role"), Is.EqualTo("textbox"));
        Assert.That(content.GetAttribute("aria-multiline"), Is.EqualTo("true"));
        Assert.That(content.GetAttribute("aria-label"), Is.EqualTo("Notes"));
        Assert.That(content.GetAttribute("contenteditable"), Is.EqualTo("true"));
        Assert.That(content.GetAttribute("data-placeholder"), Is.EqualTo("Write…"));
        Assert.That(cut.Find("[role=toolbar]").GetAttribute("aria-label"), Is.EqualTo("Notes formatting"));

        var commands = cut.FindAll("[data-nx-editor-cmd]").Select(b => b.GetAttribute("data-nx-editor-cmd"));
        Assert.That(commands, Is.EqualTo(NxEditorTool.Default.Where(t => t.Name is not ("separator" or "format")).Select(t => t.Name)));
        Assert.That(cut.FindAll(".nx-toolbar-separator"), Has.Count.EqualTo(NxEditorTool.Default.Count(t => t == NxEditorTool.Separator)));
        Assert.That(cut.FindAll(".nx-editor-format"), Has.Count.EqualTo(1));
        Assert.That(cut.Find(".nx-editor").GetAttribute("style"), Is.EqualTo("height: 250px"));
    }

    [Test]
    public void Init_PassesTheValue()
    {
        Render<NxEditor>(p => p.Add(x => x.Value, "<p>Hello</p>"));

        var init = module.Invocations.Single(i => i.Identifier == "init");
        Assert.That(init.Arguments[3], Is.EqualTo("<p>Hello</p>"));
    }

    [Test]
    public void Tools_Empty_NoToolbar()
    {
        var cut = Render<NxEditor>(p => p.Add(x => x.Tools, Array.Empty<NxEditorTool>()));
        Assert.That(cut.FindAll("[role=toolbar]"), Is.Empty);
    }

    [Test]
    public void Tools_All_RendersEveryKind()
    {
        var cut = Render<NxEditor>(p => p.Add(x => x.Tools, NxEditorTool.All));

        Assert.That(cut.FindAll(".nx-editor-select"), Has.Count.EqualTo(3));
        Assert.That(cut.FindAll(".nx-editor-color"), Has.Count.EqualTo(2));
        Assert.That(cut.Find("[data-nx-editor-cmd=insertTable]").GetAttribute("aria-haspopup"), Is.EqualTo("dialog"));
        Assert.That(cut.FindAll("[data-nx-editor-cmd]"), Has.Count.EqualTo(NxEditorTool.All.Count(t => EditorIcons.For(t.Name) is not null)));
    }

    [Test]
    public void CustomTool_RendersInItsSlotOnly()
    {
        var cut = Render<NxEditor>(p => p
            .Add(x => x.Tools, [NxEditorTool.Bold, NxEditorTool.Custom("Attach"), NxEditorTool.Italic])
            .Add(x => x.CustomTools, (RenderFragment)(b =>
            {
                b.OpenComponent<NxEditorCustomTool>(0);
                b.AddComponentParameter(1, nameof(NxEditorCustomTool.Name), "Attach");
                b.AddComponentParameter(2, nameof(NxEditorCustomTool.ChildContent), (RenderFragment)(c => c.AddMarkupContent(0, "<button id=\"attach\">Attach</button>")));
                b.CloseComponent();
                b.OpenComponent<NxEditorCustomTool>(3);
                b.AddComponentParameter(4, nameof(NxEditorCustomTool.Name), "Unused");
                b.AddComponentParameter(5, nameof(NxEditorCustomTool.ChildContent), (RenderFragment)(c => c.AddMarkupContent(0, "<button id=\"unused\">X</button>")));
                b.CloseComponent();
            })));

        Assert.That(cut.FindAll("#attach"), Has.Count.EqualTo(1));
        Assert.That(cut.FindAll("#unused"), Is.Empty);
        var toolbar = cut.Find("[role=toolbar]").InnerHtml;
        Assert.That(toolbar.IndexOf("bold", StringComparison.Ordinal), Is.LessThan(toolbar.IndexOf("attach", StringComparison.Ordinal)));
        Assert.That(toolbar.IndexOf("attach", StringComparison.Ordinal), Is.LessThan(toolbar.IndexOf("italic", StringComparison.Ordinal)));
    }

    [Test]
    public async Task ContentChanged_RaisesValueChanged_OnceForTheSameHtml()
    {
        var raised = new List<string?>();
        var cut = RenderBound(raised, "<p>a</p>");

        await cut.InvokeAsync(() => cut.Instance.OnContentChanged("<p>ab</p>"));
        await cut.InvokeAsync(() => cut.Instance.OnContentChanged("<p>ab</p>"));

        Assert.That(raised, Is.EqualTo(new[] { "<p>ab</p>" }));
        // Its own value coming back is not an outside change: the text is not replaced.
        Assert.That(Calls("setHtml"), Is.Zero);
    }

    [Test]
    public void OutsideValueChange_ReplacesTheText()
    {
        var cut = Render<NxEditor>(p => p.Add(x => x.Value, "<p>a</p>"));
        cut.Render(p => p.Add(x => x.Value, "<p>b</p>"));

        var set = module.Invocations.Single(i => i.Identifier == "setHtml");
        Assert.That(set.Arguments[1], Is.EqualTo("<p>b</p>"));
    }

    [Test]
    public async Task State_PressesAndEnablesTools()
    {
        var cut = Render<NxEditor>(p => p.Add(x => x.Tools, NxEditorTool.All));
        Assert.That(cut.Find("[data-nx-editor-cmd=bold]").GetAttribute("aria-pressed"), Is.EqualTo("false"));
        Assert.That(cut.Find("[data-nx-editor-cmd=unlink]").HasAttribute("disabled"), Is.True);
        Assert.That(cut.Find("[data-nx-editor-cmd=addRowAfter]").HasAttribute("disabled"), Is.True);
        Assert.That(cut.Find("[data-nx-editor-cmd=undo]").HasAttribute("disabled"), Is.True);

        await cut.InvokeAsync(() => cut.Instance.OnStateChanged(State(new
        {
            bold = true, orderedList = true, align = "center", inLink = true, inTable = true, canUndo = true
        })));

        Assert.That(cut.Find("[data-nx-editor-cmd=bold]").GetAttribute("aria-pressed"), Is.EqualTo("true"));
        Assert.That(cut.Find("[data-nx-editor-cmd=orderedList]").GetAttribute("aria-pressed"), Is.EqualTo("true"));
        Assert.That(cut.Find("[data-nx-editor-cmd=alignCenter]").GetAttribute("aria-pressed"), Is.EqualTo("true"));
        Assert.That(cut.Find("[data-nx-editor-cmd=alignLeft]").GetAttribute("aria-pressed"), Is.EqualTo("false"));
        Assert.That(cut.Find("[data-nx-editor-cmd=unlink]").HasAttribute("disabled"), Is.False);
        Assert.That(cut.Find("[data-nx-editor-cmd=addRowAfter]").HasAttribute("disabled"), Is.False);
        Assert.That(cut.Find("[data-nx-editor-cmd=undo]").HasAttribute("disabled"), Is.False);
        Assert.That(cut.Find("[data-nx-editor-cmd=redo]").HasAttribute("disabled"), Is.True);
    }

    [Test]
    public async Task Button_ExecutesItsCommand()
    {
        var cut = Render<NxEditor>(p => p.Add(x => x.Id, "ed"));

        await cut.Find("[data-nx-editor-cmd=italic]").ClickAsync(new MouseEventArgs());

        var exec = module.Invocations.Single(i => i.Identifier == "exec");
        Assert.That(exec.Arguments, Is.EqualTo(new object?[] { "ed", "italic", null }));
    }

    [Test]
    public async Task Link_DialogInsertsTheLink()
    {
        var cut = Render<NxEditor>(p => p.Add(x => x.Id, "ed"));

        await cut.Find("[data-nx-editor-cmd=createLink]").ClickAsync(new MouseEventArgs());
        Assert.That(Calls("linkInfo"), Is.EqualTo(1));
        cut.Find("#ed-link-href").Input("https://nep.example/pours");
        cut.Find("#ed-link-text").Input("Pours");
        await cut.FindAll(".nx-editor-dialog button").First(b => b.TextContent.Trim() == "Insert").ClickAsync(new MouseEventArgs());

        var create = module.Invocations.Single(i => i.Identifier == "createLink");
        Assert.That(create.Arguments, Is.EqualTo(new object?[] { "ed", "https://nep.example/pours", "Pours", false }));
        Assert.That(cut.FindAll("#ed-link-href"), Is.Empty);
    }

    [Test]
    public async Task CtrlK_OpensTheLinkDialog_OnlyWithTheTool()
    {
        var cut = Render<NxEditor>(p => p.Add(x => x.Id, "ed"));
        await cut.InvokeAsync(() => cut.Instance.OnShortcut("createLink"));
        Assert.That(cut.FindAll("#ed-link-href"), Has.Count.EqualTo(1));

        var bare = Render<NxEditor>(p => p.Add(x => x.Id, "ed2").Add(x => x.Tools, [NxEditorTool.Bold]));
        await bare.InvokeAsync(() => bare.Instance.OnShortcut("createLink"));
        Assert.That(bare.FindAll("#ed2-link-href"), Is.Empty);
    }

    [Test]
    public async Task Image_DialogInsertsThePicture()
    {
        var cut = Render<NxEditor>(p => p.Add(x => x.Id, "ed").Add(x => x.Tools, [NxEditorTool.InsertImage]));

        await cut.Find("[data-nx-editor-cmd=insertImage]").ClickAsync(new MouseEventArgs());
        cut.Find("#ed-image-src").Input("https://nep.example/wp12.png");
        cut.Find("#ed-image-alt").Input("Panel WP-12");
        await cut.FindAll(".nx-editor-dialog button").First(b => b.TextContent.Trim() == "Insert").ClickAsync(new MouseEventArgs());

        var insert = module.Invocations.Single(i => i.Identifier == "insertImage");
        Assert.That(insert.Arguments.Take(3), Is.EqualTo(new object?[] { "ed", "https://nep.example/wp12.png", "Panel WP-12" }));
    }

    [Test]
    public async Task TablePicker_OpensAndInsertsTheSize()
    {
        var cut = Render<NxEditor>(p => p.Add(x => x.Id, "ed").Add(x => x.Tools, [NxEditorTool.InsertTable]));

        await cut.Find("[data-nx-editor-cmd=insertTable]").ClickAsync(new MouseEventArgs());
        Assert.That(cut.FindAll(".nx-editor-tablepicker-cell"), Has.Count.EqualTo(64));
        Assert.That(cut.Find(".nx-editor-tablepicker").GetAttribute("role"), Is.EqualTo("dialog"));
        Assert.That(Calls("openTablePicker"), Is.EqualTo(1));

        await cut.InvokeAsync(() => cut.Instance.OnTablePicked(3, 4));

        Assert.That(cut.FindAll(".nx-editor-tablepicker"), Is.Empty);
        Assert.That(Calls("closeTablePicker"), Is.EqualTo(1));
        var insert = module.Invocations.Single(i => i.Identifier == "insertTable");
        Assert.That(insert.Arguments, Is.EqualTo(new object?[] { "ed", 3, 4 }));
    }

    [Test]
    public async Task InsertHtmlAsync_GoesToTheModule()
    {
        var cut = Render<NxEditor>(p => p.Add(x => x.Id, "ed"));
        await cut.InvokeAsync(() => cut.Instance.InsertHtmlAsync("<p>Signed</p>"));

        var insert = module.Invocations.Single(i => i.Identifier == "insertHtml");
        Assert.That(insert.Arguments, Is.EqualTo(new object?[] { "ed", "<p>Signed</p>" }));
    }

    [Test]
    public void Disabled_NotEditable_ToolsOff()
    {
        var cut = Render<NxEditor>(p => p.Add(x => x.Id, "ed").Add(x => x.Disabled, true));

        var content = cut.Find("#ed");
        Assert.That(content.GetAttribute("contenteditable"), Is.EqualTo("false"));
        Assert.That(content.GetAttribute("aria-disabled"), Is.EqualTo("true"));
        Assert.That(content.GetAttribute("tabindex"), Is.EqualTo("-1"));
        Assert.That(cut.FindAll("[data-nx-editor-cmd]").All(b => b.HasAttribute("disabled")), Is.True);
        Assert.That(cut.Find(".nx-editor").ClassList, Does.Contain("nx-editor-disabled"));
    }

    [Test]
    public void ReadOnly_NotEditable_StillFocusable()
    {
        var cut = Render<NxEditor>(p => p.Add(x => x.Id, "ed").Add(x => x.ReadOnly, true));

        var content = cut.Find("#ed");
        Assert.That(content.GetAttribute("contenteditable"), Is.EqualTo("false"));
        Assert.That(content.GetAttribute("aria-readonly"), Is.EqualTo("true"));
        Assert.That(content.HasAttribute("tabindex"), Is.False);
    }

    [Test]
    public void Height_Auto_AddsTheClass()
    {
        var cut = Render<NxEditor>(p => p.Add(x => x.Height, "auto").Add(x => x.Style, "width: 300px"));

        var outer = cut.Find(".nx-editor");
        Assert.That(outer.ClassList, Does.Contain("nx-editor-auto"));
        Assert.That(outer.GetAttribute("style"), Is.EqualTo("height: auto; width: 300px"));
    }

    [Test]
    public void Prerender_ShowsTheValueAsMarkup()
    {
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        var cut = Render<NxEditor>(p => p.Add(x => x.Id, "ed").Add(x => x.Value, "<p><strong>Hi</strong></p>"));

        var content = cut.Find("#ed");
        Assert.That(content.InnerHtml, Is.EqualTo("<p><strong>Hi</strong></p>"));
        Assert.That(content.GetAttribute("contenteditable"), Is.EqualTo("false"));
    }

    [Test]
    public async Task Validation_InvalidMarksTheEditor()
    {
        var model = new Note();
        var context = new EditContext(model);
        var store = new ValidationMessageStore(context);
        var cut = Render<CascadingValue<EditContext>>(p => p
            .Add(x => x.Value, context)
            .Add(x => x.IsFixed, true)
            .AddChildContent<NxEditor>(e => e
                .Add(x => x.Id, "ed")
                .Add(x => x.Value, model.Text)
                .Add(x => x.ValueExpression, () => model.Text)));

        await cut.InvokeAsync(() =>
        {
            store.Add(new FieldIdentifier(model, nameof(Note.Text)), "Required");
            context.NotifyValidationStateChanged();
        });

        Assert.That(cut.Find("#ed").GetAttribute("aria-invalid"), Is.EqualTo("true"));
        Assert.That(cut.Find(".nx-editor").ClassList, Does.Contain("nx-editor-invalid"));
    }

    private sealed class Note
    {
        [Required]
        public string? Text { get; set; }
    }
}
