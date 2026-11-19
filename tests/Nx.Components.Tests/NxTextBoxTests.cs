using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;

namespace Nx.Components.Tests;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class NxTextBoxTests : BunitContext
{
    private class Model
    {
        public string? Name { get; set; }
    }

    [SetUp]
    public void SetUp() => JSInterop.Mode = JSRuntimeMode.Loose;

    private static RenderFragment Markup(string html) => b => b.AddMarkupContent(0, html);

    private static Task TypeAsync(IRenderedComponent<NxTextBox> cut, string text) =>
        cut.Find("input").TriggerEventAsync("oninput", new ChangeEventArgs { Value = text });

    /// <summary>Dispatches an input event without waiting for a debounced handler; find and dispatch run as one step.</summary>
    private static Task StartTypingAsync(IRenderedComponent<NxTextBox> cut, string text) =>
        cut.InvokeAsync(() => { _ = cut.Find("input").TriggerEventAsync("oninput", new ChangeEventArgs { Value = text }); });

    private static Task ChangeAsync(IRenderedComponent<NxTextBox> cut, string text) =>
        cut.Find("input").TriggerEventAsync("onchange", new ChangeEventArgs { Value = text });

    /// <summary>Renders a text box whose ValueChanged writes back into <paramref name="values"/>, like @bind-Value.</summary>
    private IRenderedComponent<NxTextBox> RenderBound(List<string?> values, Action<ComponentParameterCollectionBuilder<NxTextBox>>? configure = null, string? initial = null)
    {
        IRenderedComponent<NxTextBox>? cut = null;
        cut = Render<NxTextBox>(p =>
        {
            p.Add(x => x.Value, initial);
            p.Add(x => x.ValueChanged, (string? v) =>
            {
                values.Add(v);
                cut!.Render(q => q.Add(x => x.Value, v));
            });
            configure?.Invoke(p);
        });
        return cut;
    }

    // ── Rendering ─────────────────────────────────────────────────────────────

    [Test]
    public void Render_Defaults_TextInputInsideField()
    {
        var cut = Render<NxTextBox>();

        var input = cut.Find(".nx-textbox .nx-textbox-field input.nx-textbox-input");
        Assert.That(input.GetAttribute("type"), Is.EqualTo("text"));
        Assert.That(input.GetAttribute("value"), Is.EqualTo(""));
        Assert.That(cut.FindAll("label"), Is.Empty);
        Assert.That(cut.FindAll(".nx-textbox-clear"), Is.Empty);
        Assert.That(cut.FindAll(".nx-textbox-counter"), Is.Empty);
    }

    [Test]
    public void Render_Value_ShownInInput()
    {
        var cut = Render<NxTextBox>(p => p.Add(x => x.Value, "Acme"));

        Assert.That(cut.Find("input").GetAttribute("value"), Is.EqualTo("Acme"));
    }

    [Test]
    public void Label_PointsAtGeneratedInputId()
    {
        var cut = Render<NxTextBox>(p => p.Add(x => x.Label, "Customer"));

        var label = cut.Find("label.nx-textbox-label");
        var id = cut.Find("input").Id;
        Assert.That(label.TextContent, Is.EqualTo("Customer"));
        Assert.That(id, Does.StartWith("nx-textbox-"));
        Assert.That(label.GetAttribute("for"), Is.EqualTo(id));
    }

    [Test]
    public void Id_OverridesGeneratedId()
    {
        var cut = Render<NxTextBox>(p => p
            .Add(x => x.Id, "customer")
            .Add(x => x.Label, "Customer"));

        Assert.That(cut.Find("input").Id, Is.EqualTo("customer"));
        Assert.That(cut.Find("label").GetAttribute("for"), Is.EqualTo("customer"));
    }

    [Test]
    public void GeneratedIds_AreUniquePerInstance()
    {
        var a = Render<NxTextBox>().Find("input").Id;
        var b = Render<NxTextBox>().Find("input").Id;

        Assert.That(a, Is.Not.EqualTo(b));
    }

    [Test]
    public void Placeholder_SetsAttribute()
    {
        var cut = Render<NxTextBox>(p => p.Add(x => x.Placeholder, "Search…"));

        Assert.That(cut.Find("input").GetAttribute("placeholder"), Is.EqualTo("Search…"));
    }

    [Test]
    public void PrefixAndSuffix_RenderAroundInput()
    {
        var cut = Render<NxTextBox>(p => p
            .Add(x => x.Prefix, Markup("<b>$</b>"))
            .Add(x => x.Suffix, Markup("<i>USD</i>")));

        var field = cut.Find(".nx-textbox-field");
        Assert.That(field.Children[0].ClassList, Does.Contain("nx-textbox-prefix"));
        Assert.That(field.Children[1].ClassList, Does.Contain("nx-textbox-control"));
        Assert.That(field.Children[2].ClassList, Does.Contain("nx-textbox-suffix"));
        Assert.That(cut.Find(".nx-textbox-prefix b").TextContent, Is.EqualTo("$"));
        Assert.That(cut.Find(".nx-textbox").ClassList, Does.Contain("nx-textbox-has-prefix"));
    }

    // ── Parameters ────────────────────────────────────────────────────────────

    [TestCase(NxTextBoxType.Text,     "text")]
    [TestCase(NxTextBoxType.Password, "password")]
    [TestCase(NxTextBoxType.Email,    "email")]
    [TestCase(NxTextBoxType.Search,   "search")]
    [TestCase(NxTextBoxType.Tel,      "tel")]
    [TestCase(NxTextBoxType.Url,      "url")]
    public void Type_SetsInputType(NxTextBoxType type, string expected)
    {
        var cut = Render<NxTextBox>(p => p.Add(x => x.Type, type));

        Assert.That(cut.Find("input").GetAttribute("type"), Is.EqualTo(expected));
    }

    [TestCase(NxInputMode.Text,    "text")]
    [TestCase(NxInputMode.Decimal, "decimal")]
    [TestCase(NxInputMode.Numeric, "numeric")]
    [TestCase(NxInputMode.Tel,     "tel")]
    [TestCase(NxInputMode.Search,  "search")]
    [TestCase(NxInputMode.Email,   "email")]
    [TestCase(NxInputMode.Url,     "url")]
    [TestCase(NxInputMode.None,    "none")]
    public void InputMode_SetsLowercaseAttribute(NxInputMode mode, string expected)
    {
        var cut = Render<NxTextBox>(p => p.Add(x => x.InputMode, mode));

        Assert.That(cut.Find("input").GetAttribute("inputmode"), Is.EqualTo(expected));
    }

    [Test]
    public void InputMode_Null_OmitsAttribute()
    {
        var cut = Render<NxTextBox>();

        Assert.That(cut.Find("input").HasAttribute("inputmode"), Is.False);
    }

    [Test]
    public void AutoCompleteAndMaxLength_SetAttributes()
    {
        var cut = Render<NxTextBox>(p => p
            .Add(x => x.AutoComplete, "off")
            .Add(x => x.MaxLength, 20));

        var input = cut.Find("input");
        Assert.That(input.GetAttribute("autocomplete"), Is.EqualTo("off"));
        Assert.That(input.GetAttribute("maxlength"), Is.EqualTo("20"));
    }

    [Test]
    public void Disabled_SetsAttributeAndClass()
    {
        var cut = Render<NxTextBox>(p => p.Add(x => x.Disabled, true));

        Assert.That(cut.Find("input").HasAttribute("disabled"), Is.True);
        Assert.That(cut.Find(".nx-textbox").ClassList, Does.Contain("nx-textbox-disabled"));
    }

    [Test]
    public void ReadOnly_SetsAttributeAndClass()
    {
        var cut = Render<NxTextBox>(p => p.Add(x => x.ReadOnly, true));

        Assert.That(cut.Find("input").HasAttribute("readonly"), Is.True);
        Assert.That(cut.Find("input").HasAttribute("disabled"), Is.False);
        Assert.That(cut.Find(".nx-textbox").ClassList, Does.Contain("nx-textbox-readonly"));
    }

    [Test]
    public void ClassAndStyle_ApplyToOuterElement()
    {
        var cut = Render<NxTextBox>(p => p
            .Add(x => x.Class, "wide")
            .Add(x => x.Style, "width: 300px"));

        var outer = cut.Find(".nx-textbox");
        Assert.That(outer.ClassList, Does.Contain("wide"));
        Assert.That(outer.GetAttribute("style"), Is.EqualTo("width: 300px"));
    }

    [Test]
    public void AdditionalAttributes_PassThroughToInput()
    {
        var cut = Render<NxTextBox>(p => p
            .AddUnmatched("name", "customer")
            .AddUnmatched("aria-label", "Customer name")
            .AddUnmatched("pattern", "[A-Z]+"));

        var input = cut.Find("input");
        Assert.That(input.GetAttribute("name"), Is.EqualTo("customer"));
        Assert.That(input.GetAttribute("aria-label"), Is.EqualTo("Customer name"));
        Assert.That(input.GetAttribute("pattern"), Is.EqualTo("[A-Z]+"));
    }

    // ── Binding modes ─────────────────────────────────────────────────────────

    [Test]
    public async Task OnChangeMode_InputDoesNotRaiseValueChanged_ChangeDoes()
    {
        var values = new List<string?>();
        var cut = RenderBound(values);

        await TypeAsync(cut, "Ac");
        Assert.That(values, Is.Empty);
        Assert.That(cut.Find("input").GetAttribute("value"), Is.EqualTo("Ac"));

        await ChangeAsync(cut, "Acme");
        Assert.That(values, Is.EqualTo(new[] { "Acme" }));
    }

    [Test]
    public async Task OnChangeMode_ParentRerenderWithSameValue_KeepsTypedText()
    {
        var values = new List<string?>();
        var cut = RenderBound(values, initial: "Old");

        await TypeAsync(cut, "Typing");
        cut.Render(p => p.Add(x => x.Value, "Old"));

        Assert.That(cut.Find("input").GetAttribute("value"), Is.EqualTo("Typing"));
    }

    [Test]
    public void ExternalValueChange_ReplacesText()
    {
        var cut = Render<NxTextBox>(p => p.Add(x => x.Value, "One"));

        cut.Render(p => p.Add(x => x.Value, "Two"));

        Assert.That(cut.Find("input").GetAttribute("value"), Is.EqualTo("Two"));
    }

    [Test]
    public async Task Immediate_EachInputRaisesValueChanged()
    {
        var values = new List<string?>();
        var cut = RenderBound(values, p => p.Add(x => x.Immediate, true));

        await TypeAsync(cut, "A");
        await TypeAsync(cut, "Ac");

        Assert.That(values, Is.EqualTo(new[] { "A", "Ac" }));
    }

    [Test]
    public async Task Immediate_ChangeAfterInput_DoesNotRaiseTwice()
    {
        var values = new List<string?>();
        var cut = RenderBound(values, p => p.Add(x => x.Immediate, true));

        await TypeAsync(cut, "Acme");
        await ChangeAsync(cut, "Acme");

        Assert.That(values, Is.EqualTo(new[] { "Acme" }));
    }

    [Test]
    public async Task Debounce_RaisesOnceWithLastTextAfterDelay()
    {
        var values = new List<string?>();
        var cut = RenderBound(values, p => p
            .Add(x => x.Immediate, true)
            .Add(x => x.DebounceDelay, 80));

        await StartTypingAsync(cut, "A");
        await StartTypingAsync(cut, "Ac");
        await StartTypingAsync(cut, "Acm");

        Assert.That(values, Is.Empty);
        cut.WaitForState(() => values.Count > 0, TimeSpan.FromSeconds(2));
        Assert.That(values, Is.EqualTo(new[] { "Acm" }));
    }

    [Test]
    public async Task Debounce_ChangeFlushesPendingValueImmediately()
    {
        var values = new List<string?>();
        var cut = RenderBound(values, p => p
            .Add(x => x.Immediate, true)
            .Add(x => x.DebounceDelay, 5000));

        await StartTypingAsync(cut, "Acme");
        await ChangeAsync(cut, "Acme");

        Assert.That(values, Is.EqualTo(new[] { "Acme" }));
    }

    [Test]
    public async Task Debounce_PendingValueKeptAcrossParentRerender()
    {
        var values = new List<string?>();
        var cut = RenderBound(values, p => p
            .Add(x => x.Immediate, true)
            .Add(x => x.DebounceDelay, 80), initial: "");

        await StartTypingAsync(cut, "Acme");
        cut.Render(p => p.Add(x => x.Value, ""));

        Assert.That(cut.Find("input").GetAttribute("value"), Is.EqualTo("Acme"));
        cut.WaitForState(() => values.Count > 0, TimeSpan.FromSeconds(2));
        Assert.That(values, Is.EqualTo(new[] { "Acme" }));
    }

    [Test]
    public async Task Debounce_OutsideValueChange_DropsPendingText()
    {
        var values = new List<string?>();
        var cut = RenderBound(values, p => p
            .Add(x => x.Immediate, true)
            .Add(x => x.DebounceDelay, 80), initial: "");

        await StartTypingAsync(cut, "Acme");
        cut.Render(p => p.Add(x => x.Value, "Reset"));
        await Task.Delay(250);

        Assert.That(values, Is.Empty);
        Assert.That(cut.Find("input").GetAttribute("value"), Is.EqualTo("Reset"));
    }

    [Test]
    public async Task Debounce_ClearWhilePending_DoesNotRaiseTypedTextLater()
    {
        var values = new List<string?>();
        var cut = RenderBound(values, p => p
            .Add(x => x.Immediate, true)
            .Add(x => x.Clearable, true)
            .Add(x => x.DebounceDelay, 80), initial: "A");

        await StartTypingAsync(cut, "Acme");
        await cut.Find(".nx-textbox-clear").ClickAsync(new MouseEventArgs());
        await Task.Delay(250);

        Assert.That(values, Is.EqualTo(new[] { "" }));
    }

    // ── Clear button ──────────────────────────────────────────────────────────

    [Test]
    public void Clearable_HiddenWhileEmpty()
    {
        var cut = Render<NxTextBox>(p => p.Add(x => x.Clearable, true));

        Assert.That(cut.FindAll(".nx-textbox-clear"), Is.Empty);
    }

    [Test]
    public async Task Clearable_AppearsWhenTyping()
    {
        var cut = Render<NxTextBox>(p => p.Add(x => x.Clearable, true));

        await TypeAsync(cut, "x");

        Assert.That(cut.FindAll(".nx-textbox-clear"), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Clearable_Click_ClearsRaisesEmptyAndFocuses()
    {
        var values = new List<string?>();
        var cut = RenderBound(values, p => p.Add(x => x.Clearable, true), initial: "Acme");

        await cut.Find(".nx-textbox-clear").ClickAsync(new MouseEventArgs());

        Assert.That(values, Is.EqualTo(new[] { "" }));
        Assert.That(cut.Find("input").GetAttribute("value"), Is.EqualTo(""));
        Assert.That(cut.FindAll(".nx-textbox-clear"), Is.Empty);
        JSInterop.VerifyFocusAsyncInvoke();
    }

    [TestCase(true, false)]
    [TestCase(false, true)]
    public void Clearable_HiddenWhenDisabledOrReadOnly(bool disabled, bool readOnly)
    {
        var cut = Render<NxTextBox>(p => p
            .Add(x => x.Value, "Acme")
            .Add(x => x.Clearable, true)
            .Add(x => x.Disabled, disabled)
            .Add(x => x.ReadOnly, readOnly));

        Assert.That(cut.FindAll(".nx-textbox-clear"), Is.Empty);
    }

    [Test]
    public async Task Keyboard_Escape_ClearsWhenClearable()
    {
        var values = new List<string?>();
        var cut = RenderBound(values, p => p.Add(x => x.Clearable, true), initial: "Acme");

        await cut.Find("input").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });

        Assert.That(values, Is.EqualTo(new[] { "" }));
    }

    [Test]
    public void Clearable_JsMarksTheClearingEscapeAsUsed()
    {
        var module = JSInterop.SetupModule("./_content/Nx.Components/nx-input.js");
        module.Mode = JSRuntimeMode.Loose;

        var cut = Render<NxTextBox>(p => p.Add(x => x.Clearable, true));
        cut.Render(p => p.Add(x => x.Value, "Acme"));

        Assert.That(module.Invocations.Count(i => i.Identifier == "escapeClears"), Is.EqualTo(1));
    }

    [Test]
    public void Clearable_TurnedOnLater_JsIsAttachedThen()
    {
        var module = JSInterop.SetupModule("./_content/Nx.Components/nx-input.js");
        module.Mode = JSRuntimeMode.Loose;

        var cut = Render<NxTextBox>();
        Assert.That(JSInterop.Invocations, Is.Empty);

        cut.Render(p => p.Add(x => x.Clearable, true));

        Assert.That(module.Invocations.Count(i => i.Identifier == "escapeClears"), Is.EqualTo(1));
    }

    [Test]
    public async Task Keyboard_Escape_NotClearable_DoesNothing()
    {
        var values = new List<string?>();
        var cut = RenderBound(values, initial: "Acme");

        await cut.Find("input").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });

        Assert.That(values, Is.Empty);
        Assert.That(cut.Find("input").GetAttribute("value"), Is.EqualTo("Acme"));
    }

    [Test]
    public async Task Keyboard_OtherKeys_DoNotClear()
    {
        var values = new List<string?>();
        var cut = RenderBound(values, p => p.Add(x => x.Clearable, true), initial: "Acme");

        await cut.Find("input").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        Assert.That(values, Is.Empty);
    }

    // ── Password toggle ───────────────────────────────────────────────────────

    [Test]
    public async Task PasswordToggle_RevealsAndHides()
    {
        var cut = Render<NxTextBox>(p => p.Add(x => x.Type, NxTextBoxType.Password));

        var toggle = cut.Find(".nx-textbox-password-toggle");
        Assert.That(toggle.GetAttribute("aria-pressed"), Is.EqualTo("false"));
        Assert.That(toggle.GetAttribute("aria-label"), Is.EqualTo("Show password"));

        await toggle.ClickAsync(new MouseEventArgs());
        Assert.That(cut.Find("input").GetAttribute("type"), Is.EqualTo("text"));
        Assert.That(cut.Find(".nx-textbox-password-toggle").GetAttribute("aria-pressed"), Is.EqualTo("true"));
        Assert.That(cut.Find(".nx-textbox-password-toggle").GetAttribute("aria-label"), Is.EqualTo("Hide password"));

        await cut.Find(".nx-textbox-password-toggle").ClickAsync(new MouseEventArgs());
        Assert.That(cut.Find("input").GetAttribute("type"), Is.EqualTo("password"));
    }

    [Test]
    public void PasswordToggle_Off_NotRendered()
    {
        var cut = Render<NxTextBox>(p => p
            .Add(x => x.Type, NxTextBoxType.Password)
            .Add(x => x.ShowPasswordToggle, false));

        Assert.That(cut.FindAll(".nx-textbox-password-toggle"), Is.Empty);
    }

    [Test]
    public void PasswordToggle_HiddenWhenDisabled()
    {
        var cut = Render<NxTextBox>(p => p
            .Add(x => x.Type, NxTextBoxType.Password)
            .Add(x => x.Disabled, true));

        Assert.That(cut.FindAll(".nx-textbox-password-toggle"), Is.Empty);
        Assert.That(cut.Find("input").GetAttribute("type"), Is.EqualTo("password"));
    }

    [Test]
    public void PasswordToggle_NotRenderedForTextType()
    {
        var cut = Render<NxTextBox>();

        Assert.That(cut.FindAll(".nx-textbox-password-toggle"), Is.Empty);
    }

    // ── Counter ───────────────────────────────────────────────────────────────

    [Test]
    public async Task Counter_ShowsLengthAndUpdatesWhileTyping()
    {
        var cut = Render<NxTextBox>(p => p
            .Add(x => x.Value, "abc")
            .Add(x => x.MaxLength, 10)
            .Add(x => x.ShowCounter, true));

        Assert.That(cut.Find(".nx-textbox-counter").TextContent, Is.EqualTo("3 / 10"));

        await TypeAsync(cut, "abcdef");
        Assert.That(cut.Find(".nx-textbox-counter").TextContent, Is.EqualTo("6 / 10"));
    }

    [Test]
    public void Counter_LinkedByAriaDescribedBy()
    {
        var cut = Render<NxTextBox>(p => p
            .Add(x => x.MaxLength, 10)
            .Add(x => x.ShowCounter, true));

        Assert.That(cut.Find("input").GetAttribute("aria-describedby"), Is.EqualTo(cut.Find(".nx-textbox-counter").Id));
    }

    [Test]
    public void Counter_RequiresMaxLength()
    {
        var cut = Render<NxTextBox>(p => p.Add(x => x.ShowCounter, true));

        Assert.That(cut.FindAll(".nx-textbox-counter"), Is.Empty);
        Assert.That(cut.Find("input").HasAttribute("aria-describedby"), Is.False);
    }

    // ── EditForm validation ───────────────────────────────────────────────────

    private (IRenderedComponent<NxTextBox> Cut, EditContext Context, Model Model) RenderInForm()
    {
        var model = new Model();
        var context = new EditContext(model);
        var host = Render<CascadingValue<EditContext>>(p => p
            .Add(x => x.Value, context)
            .AddChildContent<NxTextBox>(tb => tb
                .Add(x => x.Value, model.Name)
                .Add(x => x.ValueChanged, (string? v) => model.Name = v)
                .Add(x => x.ValueExpression, () => model.Name)));
        return (host.FindComponent<NxTextBox>(), context, model);
    }

    [Test]
    public async Task Validation_Change_MarksFieldModified()
    {
        var (cut, context, model) = RenderInForm();

        await ChangeAsync(cut, "Acme");

        Assert.That(model.Name, Is.EqualTo("Acme"));
        Assert.That(context.IsModified(context.Field(nameof(Model.Name))), Is.True);
        Assert.That(cut.Find("input").ClassList, Does.Contain("modified"));
    }

    [Test]
    public async Task Validation_Messages_MarkInvalid_AndClearAgain()
    {
        var (cut, context, model) = RenderInForm();
        var store = new ValidationMessageStore(context);

        store.Add(context.Field(nameof(Model.Name)), "Required");
        await cut.InvokeAsync(context.NotifyValidationStateChanged);

        Assert.That(cut.Find(".nx-textbox").ClassList, Does.Contain("nx-textbox-invalid"));
        Assert.That(cut.Find("input").ClassList, Does.Contain("invalid"));
        Assert.That(cut.Find("input").GetAttribute("aria-invalid"), Is.EqualTo("true"));

        store.Clear();
        await cut.InvokeAsync(context.NotifyValidationStateChanged);

        Assert.That(cut.Find(".nx-textbox").ClassList, Does.Not.Contain("nx-textbox-invalid"));
        Assert.That(cut.Find("input").HasAttribute("aria-invalid"), Is.False);
    }

    [Test]
    public void Validation_WithoutEditContext_NoValidationClasses()
    {
        var cut = Render<NxTextBox>(p => p.Add(x => x.Value, "x"));

        Assert.That(cut.Find("input").GetAttribute("class"), Is.EqualTo("nx-textbox-input"));
    }

    [Test]
    public async Task Validation_InsideEditForm_WorksWithValidationMessage()
    {
        var model = new Model();
        var cut = Render<EditForm>(p => p
            .Add(x => x.Model, model)
            .Add(x => x.ChildContent, (EditContext ctx) => b =>
            {
                b.OpenComponent<NxTextBox>(0);
                b.AddAttribute(1, nameof(NxTextBox.Value), model.Name);
                b.AddAttribute(2, nameof(NxTextBox.ValueExpression), (System.Linq.Expressions.Expression<Func<string?>>)(() => model.Name));
                b.CloseComponent();
                b.OpenComponent<ValidationMessage<string?>>(3);
                b.AddAttribute(4, "For", (System.Linq.Expressions.Expression<Func<string?>>)(() => model.Name));
                b.CloseComponent();
            }));

        var editContext = cut.Instance.EditContext!;
        var store = new ValidationMessageStore(editContext);
        store.Add(editContext.Field(nameof(Model.Name)), "Enter a name.");
        await cut.InvokeAsync(editContext.NotifyValidationStateChanged);

        Assert.That(cut.Find(".validation-message").TextContent, Is.EqualTo("Enter a name."));
        Assert.That(cut.Find(".nx-textbox").ClassList, Does.Contain("nx-textbox-invalid"));
    }

    // ── Focus and events ──────────────────────────────────────────────────────

    [Test]
    public void AutoFocus_FocusesAfterFirstRender()
    {
        Render<NxTextBox>(p => p.Add(x => x.AutoFocus, true));

        JSInterop.VerifyFocusAsyncInvoke();
    }

    [Test]
    public void AutoFocus_Off_DoesNotFocus()
    {
        Render<NxTextBox>();

        Assert.That(JSInterop.Invocations, Is.Empty);
    }

    [Test]
    public async Task OnFocusAndOnBlur_Raised()
    {
        var events = new List<string>();
        var cut = Render<NxTextBox>(p => p
            .Add(x => x.OnFocus, (FocusEventArgs _) => events.Add("focus"))
            .Add(x => x.OnBlur, (FocusEventArgs _) => events.Add("blur")));

        await cut.Find("input").FocusAsync(new FocusEventArgs());
        await cut.Find("input").BlurAsync(new FocusEventArgs());

        Assert.That(events, Is.EqualTo(new[] { "focus", "blur" }));
    }

    [Test]
    public async Task FocusAsync_FocusesInput()
    {
        var cut = Render<NxTextBox>();

        await cut.InvokeAsync(() => cut.Instance.FocusAsync().AsTask());

        JSInterop.VerifyFocusAsyncInvoke();
    }

    // ── Accessibility ─────────────────────────────────────────────────────────

    [Test]
    public void Accessibility_ClearButtonIsLabelledAndOutOfTabOrder()
    {
        var cut = Render<NxTextBox>(p => p
            .Add(x => x.Value, "x")
            .Add(x => x.Clearable, true));

        var clear = cut.Find(".nx-textbox-clear");
        Assert.That(clear.GetAttribute("type"), Is.EqualTo("button"));
        Assert.That(clear.GetAttribute("aria-label"), Is.EqualTo("Clear"));
        Assert.That(clear.GetAttribute("tabindex"), Is.EqualTo("-1"));
        Assert.That(clear.GetAttribute("aria-controls"), Is.EqualTo(cut.Find("input").Id));
        Assert.That(clear.QuerySelector("svg")!.GetAttribute("aria-hidden"), Is.EqualTo("true"));
    }

    [Test]
    public void Accessibility_PasswordToggleIsFocusableAndControlsInput()
    {
        var cut = Render<NxTextBox>(p => p.Add(x => x.Type, NxTextBoxType.Password));

        var toggle = cut.Find(".nx-textbox-password-toggle");
        Assert.That(toggle.GetAttribute("type"), Is.EqualTo("button"));
        Assert.That(toggle.HasAttribute("tabindex"), Is.False);
        Assert.That(toggle.GetAttribute("aria-controls"), Is.EqualTo(cut.Find("input").Id));
    }

    [Test]
    public void Accessibility_NoAriaInvalidByDefault()
    {
        var cut = Render<NxTextBox>();

        Assert.That(cut.Find("input").HasAttribute("aria-invalid"), Is.False);
    }
}
