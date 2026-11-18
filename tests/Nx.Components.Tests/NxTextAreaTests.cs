using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;

namespace Nx.Components.Tests;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class NxTextAreaTests : BunitContext
{
    private BunitJSModuleInterop module = default!;

    [SetUp]
    public void SetUp()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        module = JSInterop.SetupModule("./_content/Nx.Components/nx-input.js");
        module.Mode = JSRuntimeMode.Loose;
    }

    private IRenderedComponent<NxTextArea> RenderBound(List<string?> raised, string? initial = null,
        Action<ComponentParameterCollectionBuilder<NxTextArea>>? configure = null)
    {
        IRenderedComponent<NxTextArea>? cut = null;
        cut = Render<NxTextArea>(p =>
        {
            p.Add(x => x.Id, "ta");
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

    [Test]
    public void Render_TextareaInMultilineFrame()
    {
        var cut = RenderBound([], "a\nb", p => p.Add(x => x.Label, "Notes"));

        var textarea = cut.Find(".nx-field.nx-textarea.nx-field-multiline textarea#ta.nx-textarea-input");
        Assert.That(textarea.GetAttribute("rows"), Is.EqualTo("3"));
        Assert.That(textarea.GetAttribute("value") ?? textarea.TextContent, Is.EqualTo("a\nb"));
        Assert.That(cut.Find("label[for=ta]").TextContent, Is.EqualTo("Notes"));
        Assert.That(textarea.GetAttribute("data-resize"), Is.EqualTo("vertical"));
    }

    [Test]
    public void TextBox_FrameIsNotMultiline()
    {
        var cut = Render<NxTextBox>();

        Assert.That(cut.Find(".nx-field").ClassList, Does.Not.Contain("nx-field-multiline"));
    }

    [TestCase(NxTextAreaResize.None, "none")]
    [TestCase(NxTextAreaResize.Both, "both")]
    public void Resize_Attribute(NxTextAreaResize resize, string expected)
    {
        var cut = RenderBound([], configure: p => p.Add(x => x.Resize, resize));

        Assert.That(cut.Find("textarea").GetAttribute("data-resize"), Is.EqualTo(expected));
    }

    [Test]
    public void AutoResize_NoHandle_StartsAtMinRows_CallsJs()
    {
        var cut = RenderBound([], configure: p => p.Add(x => x.AutoResize, true).Add(x => x.MinRows, 2).Add(x => x.MaxRows, 6));

        Assert.That(cut.Find("textarea").GetAttribute("data-resize"), Is.EqualTo("none"));
        Assert.That(cut.Find("textarea").GetAttribute("rows"), Is.EqualTo("2"));
        var call = module.Invocations.Single(i => i.Identifier == "autosize");
        Assert.That(call.Arguments[1], Is.EqualTo(2));
        Assert.That(call.Arguments[2], Is.EqualTo(6));
    }

    [Test]
    public void AutoResize_ReMeasuresWhenValueSetFromCode()
    {
        var cut = RenderBound([], "a", p => p.Add(x => x.AutoResize, true));

        cut.Render(p => p.Add(x => x.Value, "a\nb\nc\nd"));

        Assert.That(module.Invocations.Count(i => i.Identifier == "autosize"), Is.EqualTo(2));
    }

    [Test]
    public void NoAutoResize_NoJs()
    {
        RenderBound([]);

        Assert.That(module.Invocations.Any(i => i.Identifier == "autosize"), Is.False);
    }

    [Test]
    public async Task OnChange_Commits_NotOnInput()
    {
        var raised = new List<string?>();
        var cut = RenderBound(raised);

        await cut.Find("textarea").TriggerEventAsync("oninput", new ChangeEventArgs { Value = "line 1\nline 2" });
        Assert.That(raised, Is.Empty);
        await cut.Find("textarea").TriggerEventAsync("onchange", new ChangeEventArgs { Value = "line 1\nline 2" });

        Assert.That(raised, Is.EqualTo(new[] { "line 1\nline 2" }));
    }

    [Test]
    public async Task Immediate_CommitsOnInput()
    {
        var raised = new List<string?>();
        var cut = RenderBound(raised, configure: p => p.Add(x => x.Immediate, true));

        await cut.Find("textarea").TriggerEventAsync("oninput", new ChangeEventArgs { Value = "x" });

        Assert.That(raised, Is.EqualTo(new[] { "x" }));
    }

    [Test]
    public void Debounce_WaitsForPause()
    {
        var raised = new List<string?>();
        var cut = RenderBound(raised, configure: p => p.Add(x => x.Immediate, true).Add(x => x.DebounceDelay, 50));

        cut.InvokeAsync(() => cut.Find("textarea").TriggerEventAsync("oninput", new ChangeEventArgs { Value = "a" }));
        cut.InvokeAsync(() => cut.Find("textarea").TriggerEventAsync("oninput", new ChangeEventArgs { Value = "ab" }));

        cut.WaitForState(() => raised.Count > 0);
        Assert.That(raised, Is.EqualTo(new[] { "ab" }));
    }

    [Test]
    public void Counter_ShowsLengthOfMax()
    {
        var cut = RenderBound([], "hello", p => p.Add(x => x.MaxLength, 140).Add(x => x.ShowCounter, true));

        Assert.That(cut.Find(".nx-textarea-counter").TextContent, Is.EqualTo("5 / 140"));
        Assert.That(cut.Find("textarea").GetAttribute("aria-describedby"), Is.EqualTo("ta-counter"));
        Assert.That(cut.Find("textarea").GetAttribute("maxlength"), Is.EqualTo("140"));
    }

    [Test]
    public async Task Clearable_ButtonAndEscape()
    {
        var raised = new List<string?>();
        var cut = RenderBound(raised, "notes", p => p.Add(x => x.Clearable, true));

        await cut.Find(".nx-field-clear").ClickAsync(new MouseEventArgs());
        Assert.That(raised, Is.EqualTo(new[] { "" }));

        cut.Render(p => p.Add(x => x.Value, "again"));
        await cut.Find("textarea").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });
        Assert.That(raised, Is.EqualTo(new[] { "", "" }));
    }

    [Test]
    public void Rows_SetsHeightInLines()
    {
        var cut = RenderBound([], configure: p => p.Add(x => x.Rows, 6));

        Assert.That(cut.Find("textarea").GetAttribute("rows"), Is.EqualTo("6"));
    }

    [Test]
    public async Task Counter_UpdatesWhileTyping()
    {
        var cut = RenderBound([], "hi", p => p.Add(x => x.MaxLength, 140).Add(x => x.ShowCounter, true));

        await cut.Find("textarea").TriggerEventAsync("oninput", new ChangeEventArgs { Value = "hi\nthere" });

        Assert.That(cut.Find(".nx-textarea-counter").TextContent, Is.EqualTo("8 / 140"));
    }

    [Test]
    public async Task OnChange_ParentRerenderWithSameValue_KeepsTypedText()
    {
        var cut = RenderBound([], "old");

        await cut.Find("textarea").TriggerEventAsync("oninput", new ChangeEventArgs { Value = "typing" });
        cut.Render(p => p.Add(x => x.Value, "old"));

        Assert.That(cut.Find("textarea").GetAttribute("value"), Is.EqualTo("typing"));
    }

    [Test]
    public void OutsideValueChange_ReplacesText()
    {
        var cut = RenderBound([], "one");

        cut.Render(p => p.Add(x => x.Value, "two"));

        Assert.That(cut.Find("textarea").GetAttribute("value"), Is.EqualTo("two"));
    }

    [Test]
    public void Disabled_DisablesTextarea_NoClearButton()
    {
        var cut = RenderBound([], "notes", p => p.Add(x => x.Disabled, true).Add(x => x.Clearable, true));

        Assert.That(cut.Find("textarea").HasAttribute("disabled"), Is.True);
        Assert.That(cut.FindAll(".nx-field-clear"), Is.Empty);
    }

    [Test]
    public void ReadOnly_RendersClassAndAttribute()
    {
        var cut = RenderBound([], "x", p => p.Add(x => x.Label, "Notes").Add(x => x.ReadOnly, true));

        Assert.That(cut.Find(".nx-field").ClassList, Does.Contain("nx-field-readonly"));
        Assert.That(cut.Find("textarea").HasAttribute("readonly"), Is.True);
    }

    private class Model
    {
        public string? Notes { get; set; }
    }

    [Test]
    public async Task Validation_ChangeMarksModified_MessagesMarkInvalid()
    {
        var model = new Model();
        var context = new EditContext(model);
        var host = Render<CascadingValue<EditContext>>(p => p
            .Add(x => x.Value, context)
            .AddChildContent<NxTextArea>(c => c
                .Add(x => x.Value, model.Notes)
                .Add(x => x.ValueChanged, (string? v) => model.Notes = v)
                .Add(x => x.ValueExpression, () => model.Notes)));
        var cut = host.FindComponent<NxTextArea>();

        await cut.Find("textarea").TriggerEventAsync("onchange", new ChangeEventArgs { Value = "x" });
        Assert.That(context.IsModified(context.Field(nameof(Model.Notes))), Is.True);

        var store = new ValidationMessageStore(context);
        store.Add(context.Field(nameof(Model.Notes)), "Too short.");
        await cut.InvokeAsync(context.NotifyValidationStateChanged);

        Assert.That(cut.Find("textarea").GetAttribute("aria-invalid"), Is.EqualTo("true"));
        Assert.That(cut.Find(".nx-field").ClassList, Does.Contain("nx-field-invalid"));
    }
}
