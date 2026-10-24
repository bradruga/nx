using System.ComponentModel.DataAnnotations;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;

namespace Nx.Components.Tests;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class NxFormTests : BunitContext
{
    public enum Status { Planned, OnHold, Poured }

    private sealed class Pour
    {
        [Required(ErrorMessage = "Name is required.")]
        public string? Name { get; set; }

        [Display(Name = "Volume (yd³)")]
        public decimal Volume { get; set; } = 12m;

        [System.ComponentModel.DataAnnotations.Range(1, 9, ErrorMessage = "1 to 9.")]
        public int Trucks { get; set; } = 2;

        public int? Crew { get; set; }

        public bool PumpTruck { get; set; }

        public DateOnly? PourDate { get; set; }

        public DateTime Ordered { get; set; } = new(2026, 3, 5);

        public TimeOnly? StartTime { get; set; }

        public Status Status { get; set; } = Status.OnHold;

        public Status? NextStatus { get; set; }

        public string PONumber { get; set; } = "";

        public Guid Key { get; set; }
    }

    [SetUp]
    public void SetUp()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        foreach (var path in new[] { "nx-combobox.js", "nx-picker.js", "nx-input.js" })
            JSInterop.SetupModule($"./_content/Nx.Components/{path}").Mode = JSRuntimeMode.Loose;
    }

    /// <summary>Items as markup would declare them: one NxFormItem per entry, with its parameters.</summary>
    private static RenderFragment Items(params (string Field, object? Extra)[] items) => builder =>
    {
        var seq = 0;
        foreach (var (field, extra) in items)
        {
            builder.OpenComponent<NxFormItem>(seq++);
            builder.AddComponentParameter(seq++, nameof(NxFormItem.Field), field);
            if (extra is not null)
            {
                foreach (var property in extra.GetType().GetProperties())
                    builder.AddComponentParameter(seq++, property.Name, property.GetValue(extra));
            }
            builder.CloseComponent();
        }
    };

    private static RenderFragment Fields(params string[] fields) => Items(fields.Select(f => (f, (object?)null)).ToArray());

    private IRenderedComponent<NxForm> RenderForm(Pour model, RenderFragment items, Action<ComponentParameterCollectionBuilder<NxForm>>? configure = null) =>
        Render<NxForm>(p =>
        {
            p.Add(x => x.Model, model);
            p.Add(x => x.Id, "f");
            p.Add(x => x.ChildContent, items);
            configure?.Invoke(p);
        });

    // ── The form ──────────────────────────────────────────────────────────────

    [Test]
    public void Render_FormGridBodyAndButtons()
    {
        var cut = RenderForm(new Pour(), Fields("Name"), p => p
            .Add(x => x.Columns, 3)
            .Add(x => x.ColumnSpacing, "24px")
            .Add(x => x.Class, "mine")
            .Add(x => x.Buttons, (RenderFragment)(b => b.AddMarkupContent(0, "<button id=\"save\">Save</button>"))));

        var form = cut.Find("form");
        Assert.That(form.Id, Is.EqualTo("f"));
        Assert.That(form.ClassList, Does.Contain("nx-form").And.Contain("mine"));
        Assert.That(cut.Find(".nx-form-body > .nx-form-items").GetAttribute("style"),
            Is.EqualTo("--nx-form-columns: 3; --nx-form-column-gap: 24px"));
        Assert.That(cut.Find(".nx-form-body > .nx-form-buttons #save").TextContent, Is.EqualTo("Save"));
    }

    [Test]
    public void ModelOrEditContext_ExactlyOne()
    {
        Assert.Throws<InvalidOperationException>(() => Render<NxForm>());
        Assert.Throws<InvalidOperationException>(() => Render<NxForm>(p => p
            .Add(x => x.Model, new Pour())
            .Add(x => x.EditContext, new EditContext(new Pour()))));
    }

    [Test]
    public void EditContext_ReadsBackTheContextMadeForModel()
    {
        var model = new Pour();
        var cut = RenderForm(model, Fields("Name"));
        Assert.That(cut.Instance.EditContext, Is.Not.Null);
        Assert.That(cut.Instance.EditContext!.Model, Is.SameAs(model));

        var mine = new EditContext(new Pour());
        var withContext = Render<NxForm>(p => p.Add(x => x.EditContext, mine).Add(x => x.ChildContent, Fields("Name")));
        Assert.That(withContext.Instance.EditContext, Is.SameAs(mine));
    }

    [Test]
    public void Horizontal_MarksFormAndItems()
    {
        var cut = RenderForm(new Pour(), Fields("Name"), p => p.Add(x => x.Orientation, NxOrientation.Horizontal));
        Assert.That(cut.Find("form").ClassList, Does.Contain("nx-form-horizontal"));
        Assert.That(cut.Find(".nx-form-item").ClassList, Does.Contain("nx-form-item-horizontal"));
    }

    // ── Generated editors ─────────────────────────────────────────────────────

    [TestCase("Name", ".nx-textbox")]
    [TestCase("PONumber", ".nx-textbox")]
    [TestCase("Volume", ".nx-numeric")]
    [TestCase("Trucks", ".nx-numeric")]
    [TestCase("Crew", ".nx-numeric")]
    [TestCase("PumpTruck", ".nx-checkbox")]
    [TestCase("PourDate", ".nx-datepicker")]
    [TestCase("Ordered", ".nx-datepicker")]
    [TestCase("StartTime", ".nx-timepicker")]
    [TestCase("Status", ".nx-dropdown")]
    [TestCase("NextStatus", ".nx-dropdown")]
    public void Editor_PerPropertyType(string field, string selector)
    {
        var cut = RenderForm(new Pour(), Fields(field));
        Assert.That(cut.FindAll($".nx-form-item .nx-form-editor {selector}"), Has.Count.EqualTo(1));
        Assert.That(cut.Find($"#f-{field}"), Is.Not.Null);
    }

    [Test]
    public void UnsupportedType_SaysPutAnEditorInside()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => RenderForm(new Pour(), Fields("Key")));
        Assert.That(ex!.Message, Does.Contain("Guid").And.Contain("put one inside"));
    }

    [Test]
    public void UnknownField_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => RenderForm(new Pour(), Fields("Nope")));
        Assert.That(ex!.Message, Does.Contain("no public property 'Nope'"));
    }

    [Test]
    public void Label_FromLabelDisplayOrWords_PointsAtEditor()
    {
        var cut = RenderForm(new Pour(), Items(("Volume", null), ("PONumber", null), ("StartTime", null), ("Name", new { Label = "Pour name" })));

        var labels = cut.FindAll(".nx-form-label");
        Assert.That(labels.Select(l => l.TextContent), Is.EqualTo(new[] { "Volume (yd³)", "PO number", "Start time", "Pour name" }));
        Assert.That(labels.Select(l => l.GetAttribute("for")), Is.EqualTo(new[] { "f-Volume", "f-PONumber", "f-StartTime", "f-Name" }));
    }

    [Test]
    public void CheckBox_CarriesTheLabelItself()
    {
        var cut = RenderForm(new Pour(), Fields("PumpTruck"));

        Assert.That(cut.FindAll(".nx-form-label"), Is.Empty);
        Assert.That(cut.Find(".nx-form-item").ClassList, Does.Contain("nx-form-item-checkbox"));
        Assert.That(cut.Find(".nx-checkbox").TextContent, Does.Contain("Pump truck"));
    }

    [Test]
    public void Enum_ListsValuesInWords()
    {
        var cut = RenderForm(new Pour(), Fields("Status"));
        Assert.That(cut.Find("#f-Status").GetAttribute("value"), Is.EqualTo("On hold"));
    }

    [Test]
    public void Editing_WritesTheModel_AndNotifiesTheForm()
    {
        var model = new Pour();
        var cut = RenderForm(model, Fields("Name"));
        var changed = new List<string>();
        cut.Instance.EditContext!.OnFieldChanged += (_, e) => changed.Add(e.FieldIdentifier.FieldName);

        cut.Find("#f-Name").Input("North wall");
        cut.Find("#f-Name").Change("North wall");

        Assert.That(model.Name, Is.EqualTo("North wall"));
        Assert.That(changed, Does.Contain("Name"));
        Assert.That(cut.Find("#f-Name").GetAttribute("value"), Is.EqualTo("North wall"));
    }

    [Test]
    public void Disabled_DisablesGeneratedEditor()
    {
        var cut = RenderForm(new Pour(), Items(("Trucks", new { Disabled = true })));
        Assert.That(cut.Find("#f-Trucks").HasAttribute("disabled"), Is.True);
    }

    [Test]
    public void Id_OverridesEditorIdAndLabelTarget()
    {
        var cut = RenderForm(new Pour(), Items(("Name", new { Id = "pour-name" })));
        Assert.That(cut.Find(".nx-form-label").GetAttribute("for"), Is.EqualTo("pour-name"));
        Assert.That(cut.Find("#pour-name"), Is.Not.Null);
    }

    // ── Your own editor ───────────────────────────────────────────────────────

    [Test]
    public void ChildContent_ReplacesEditor_NoLabelUnlessGiven()
    {
        RenderFragment own = b => b.AddMarkupContent(0, "<input id=\"mine\" />");
        var cut = RenderForm(new Pour(), Items(("Name", new { ChildContent = own })));

        Assert.That(cut.FindAll(".nx-textbox"), Is.Empty);
        Assert.That(cut.Find(".nx-form-editor #mine"), Is.Not.Null);
        Assert.That(cut.FindAll("label.nx-form-label"), Is.Empty);

        var labelled = RenderForm(new Pour(), Items(("Name", new { ChildContent = own, Label = "Mine", Id = "mine" })));
        Assert.That(labelled.Find("label.nx-form-label").GetAttribute("for"), Is.EqualTo("mine"));
    }

    // ── Layout ────────────────────────────────────────────────────────────────

    [Test]
    public void ColSpan_ClampedToColumns()
    {
        var cut = RenderForm(new Pour(), Items(("Name", new { ColSpan = 2 }), ("Trucks", new { ColSpan = 9 }), ("Crew", null)),
            p => p.Add(x => x.Columns, 3));

        Assert.That(cut.FindAll(".nx-form-item").Select(i => i.GetAttribute("style")),
            Is.EqualTo(new[] { "--nx-form-span: 2", "--nx-form-span: 3", "--nx-form-span: 1" }));
    }

    [Test]
    public void Group_FieldsetLegendOwnColumns()
    {
        RenderFragment group = b =>
        {
            b.OpenComponent<NxFormGroup>(0);
            b.AddComponentParameter(1, nameof(NxFormGroup.Label), "Crew");
            b.AddComponentParameter(2, nameof(NxFormGroup.Columns), 4);
            b.AddComponentParameter(3, nameof(NxFormGroup.ChildContent), Items(("Trucks", new { ColSpan = 4 }), ("Crew", new { ColSpan = 6 })));
            b.CloseComponent();
        };
        var cut = RenderForm(new Pour(), group, p => p.Add(x => x.Columns, 2).Add(x => x.ColumnSpacing, "8px"));

        Assert.That(cut.Find("fieldset.nx-form-group > legend.nx-form-group-legend").TextContent, Is.EqualTo("Crew"));
        Assert.That(cut.Find(".nx-form-group-items").GetAttribute("style"), Is.EqualTo("--nx-form-columns: 4; --nx-form-column-gap: 8px"));
        Assert.That(cut.FindAll(".nx-form-group .nx-form-item").Select(i => i.GetAttribute("style")),
            Is.EqualTo(new[] { "--nx-form-span: 4", "--nx-form-span: 4" }));
    }

    // ── Validation and submit ─────────────────────────────────────────────────

    [Test]
    public async Task Submit_Invalid_ShowsMessagesUnderItems_ThenValidSubmits()
    {
        var model = new Pour { Trucks = 20 };
        var valid = 0;
        var invalid = 0;
        var cut = RenderForm(model, Fields("Name", "Trucks"), p => p
            .Add(x => x.OnValidSubmit, () => valid++)
            .Add(x => x.OnInvalidSubmit, () => invalid++));

        await cut.Find("form").SubmitAsync();
        Assert.That(invalid, Is.EqualTo(1));
        Assert.That(cut.FindAll(".nx-form-item .nx-validation-message").Select(m => m.TextContent.Trim()),
            Is.EqualTo(new[] { "Name is required.", "1 to 9." }));
        Assert.That(cut.Find("#f-Name").GetAttribute("aria-invalid"), Is.EqualTo("true"));

        model.Name = "Deck";
        model.Trucks = 3;
        await cut.Find("form").SubmitAsync();
        Assert.That(valid, Is.EqualTo(1));
        Assert.That(cut.FindAll(".nx-validation-message"), Is.Empty);
    }

    [Test]
    public async Task IsValid_ValidatesAndShowsMessages()
    {
        var cut = RenderForm(new Pour(), Fields("Name"));

        var result = true;
        await cut.InvokeAsync(() => result = cut.Instance.IsValid());

        Assert.That(result, Is.False);
        Assert.That(cut.Find(".nx-validation-message").TextContent.Trim(), Is.EqualTo("Name is required."));
    }

    [Test]
    public async Task Validation_ReplacesDataAnnotations()
    {
        RenderFragment none = _ => { };
        var cut = RenderForm(new Pour(), Fields("Name"), p => p.Add(x => x.Validation, none));

        var result = false;
        await cut.InvokeAsync(() => result = cut.Instance.IsValid());

        Assert.That(result, Is.True);
        Assert.That(cut.FindAll(".nx-validation-message"), Is.Empty);
    }

    [Test]
    public void SubmitOnEnter_False_AddsDisabledDefaultButtonFirst()
    {
        var on = RenderForm(new Pour(), Fields("Name"));
        Assert.That(on.FindAll("button.nx-form-enter-guard"), Is.Empty);

        var off = RenderForm(new Pour(), Fields("Name"), p => p.Add(x => x.SubmitOnEnter, false));
        var guard = off.Find("form button[type=submit]");
        Assert.That(guard.ClassList, Does.Contain("nx-form-enter-guard"));
        Assert.That(guard.HasAttribute("disabled"), Is.True);
        Assert.That(guard.HasAttribute("hidden"), Is.True);
    }

    // ── NxValidationMessage ───────────────────────────────────────────────────

    [Test]
    public async Task ValidationMessage_ForLambda_LiveRegion()
    {
        var model = new Pour();
        var context = new EditContext(model);
        var store = new ValidationMessageStore(context);
        var cut = Render<NxValidationMessage>(p => p
            .AddCascadingValue(context)
            .Add(x => x.For, () => model.Trucks));

        var root = cut.Find(".nx-validation");
        Assert.That(root.GetAttribute("aria-live"), Is.EqualTo("polite"));
        Assert.That(cut.FindAll(".nx-validation-message"), Is.Empty);

        store.Add(context.Field(nameof(Pour.Trucks)), "Too many.");
        store.Add(context.Field(nameof(Pour.Name)), "Not this one.");
        await cut.InvokeAsync(context.NotifyValidationStateChanged);

        Assert.That(cut.Find(".nx-validation").ClassList, Does.Contain("nx-validation-invalid"));
        Assert.That(cut.FindAll(".nx-validation-message").Select(m => m.TextContent.Trim()), Is.EqualTo(new[] { "Too many." }));
        Assert.That(cut.Find(".nx-validation-message svg").GetAttribute("aria-hidden"), Is.EqualTo("true"));
    }

    [Test]
    public async Task ValidationMessage_FieldOnTheFormModel()
    {
        var model = new Pour();
        var context = new EditContext(model);
        var store = new ValidationMessageStore(context);
        var cut = Render<NxValidationMessage>(p => p.AddCascadingValue(context).Add(x => x.Field, nameof(Pour.Name)));

        store.Add(context.Field(nameof(Pour.Name)), "Name is required.");
        await cut.InvokeAsync(context.NotifyValidationStateChanged);

        Assert.That(cut.Find(".nx-validation-message").TextContent.Trim(), Is.EqualTo("Name is required."));
    }

    // ── Label words ───────────────────────────────────────────────────────────

    [TestCase("JobStartDate", "Job start date")]
    [TestCase("PONumber", "PO number")]
    [TestCase("IsCosted", "Is costed")]
    [TestCase("Name", "Name")]
    [TestCase("Address2", "Address2")]
    [TestCase("site_contact", "site contact")]
    public void Words_SplitsPropertyNames(string name, string words) =>
        Assert.That(FormLayout.Words(name), Is.EqualTo(words));
}
