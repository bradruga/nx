using System.ComponentModel.DataAnnotations;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;

namespace Nx.Components.Tests;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class NxSignatureTests : BunitContext
{
    private const string Png = "data:image/png;base64,iVBORw0KGgo=";
    private BunitJSModuleInterop module = default!;

    [SetUp]
    public void SetUp()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        module = JSInterop.SetupModule("./_content/Nx.Components/nx-signature.js");
        module.Mode = JSRuntimeMode.Loose;
    }

    private IRenderedComponent<NxSignature> RenderBound(List<string?> raised, string? initial = null,
        Action<ComponentParameterCollectionBuilder<NxSignature>>? configure = null)
    {
        IRenderedComponent<NxSignature>? cut = null;
        cut = Render<NxSignature>(p =>
        {
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
    public void Render_EmptyPad()
    {
        var cut = Render<NxSignature>(p => p.Add(x => x.AriaLabel, "Inspector"));

        var pad = cut.Find(".nx-signature");
        Assert.That(pad.GetAttribute("role"), Is.EqualTo("group"));
        Assert.That(pad.GetAttribute("aria-label"), Is.EqualTo("Inspector"));
        Assert.That(pad.GetAttribute("style"), Is.EqualTo("width: 300px; height: 150px"));
        Assert.That(cut.Find("canvas").GetAttribute("aria-label"), Is.EqualTo("Not signed"));
        Assert.That(cut.Find(".nx-signature-placeholder").TextContent, Is.EqualTo("Sign here"));
        Assert.That(cut.FindAll(".nx-signature-clear"), Is.Empty);
    }

    [Test]
    public void Init_GetsOptionsAndValue()
    {
        Render<NxSignature>(p => p.Add(x => x.Value, Png).Add(x => x.StrokeWidth, 3).Add(x => x.Color, "#123456"));

        var init = module.Invocations.Single(i => i.Identifier == "init");
        Assert.That(init.Arguments[4], Is.EqualTo(Png));
        Assert.That(init.Arguments[3]!.ToString(), Does.Contain("strokeWidth = 3").And.Contain("color = #123456"));
    }

    [Test]
    public async Task Signed_RaisesValue_ShowsClear()
    {
        var raised = new List<string?>();
        var cut = RenderBound(raised);

        await cut.InvokeAsync(() => cut.Instance.OnSigned(Png));

        Assert.That(raised, Is.EqualTo(new[] { Png }));
        Assert.That(cut.Find("canvas").GetAttribute("aria-label"), Is.EqualTo("Signed"));
        Assert.That(cut.FindAll(".nx-signature-placeholder"), Is.Empty);
        Assert.That(cut.Find(".nx-signature-clear").GetAttribute("aria-label"), Is.EqualTo("Clear signature"));
        // Its own export coming back is not an outside change: the pad is not redrawn.
        Assert.That(module.Invocations.Count(i => i.Identifier == "setValue"), Is.Zero);
    }

    [Test]
    public async Task Clear_RaisesNull()
    {
        var raised = new List<string?>();
        var cut = RenderBound(raised, Png);

        await cut.Find(".nx-signature-clear").ClickAsync(new MouseEventArgs());

        Assert.That(raised, Is.EqualTo(new string?[] { null }));
        Assert.That(module.Invocations.Count(i => i.Identifier == "clear"), Is.EqualTo(1));
        Assert.That(cut.FindAll(".nx-signature-clear"), Is.Empty);
    }

    [Test]
    public void Init_GetsPaperColorAndExportScale()
    {
        Render<NxSignature>(p => p.Add(x => x.BackgroundColor, "#fffbe6").Add(x => x.ExportScale, 3));

        var options = module.Invocations.Single(i => i.Identifier == "init").Arguments[3]!.ToString();
        Assert.That(options, Does.Contain("background = #fffbe6").And.Contain("exportScale = 3"));
    }

    [Test]
    public void Size_PlaceholderAndClass()
    {
        var cut = Render<NxSignature>(p => p
            .Add(x => x.Width, "100%").Add(x => x.Height, "200px")
            .Add(x => x.Placeholder, "Inspector signs here")
            .Add(x => x.Class, "wide"));

        var pad = cut.Find(".nx-signature");
        Assert.That(pad.GetAttribute("style"), Is.EqualTo("width: 100%; height: 200px"));
        Assert.That(pad.ClassList, Does.Contain("wide"));
        Assert.That(cut.Find(".nx-signature-placeholder").TextContent, Is.EqualTo("Inspector signs here"));
    }

    [Test]
    public async Task Signed_NotifiesTheEditContext()
    {
        var model = new Form();
        var context = new EditContext(model);
        var changed = new List<string>();
        context.OnFieldChanged += (_, e) => changed.Add(e.FieldIdentifier.FieldName);
        var cut = Render<NxSignature>(p => p
            .AddCascadingValue(context)
            .Add(x => x.Value, model.Signature)
            .Add(x => x.ValueChanged, v => model.Signature = v)
            .Add(x => x.ValueExpression, () => model.Signature));

        await cut.InvokeAsync(() => cut.Instance.OnSigned(Png));

        Assert.That(model.Signature, Is.EqualTo(Png));
        Assert.That(changed, Is.EqualTo(new[] { nameof(Form.Signature) }));
    }

    [Test]
    public void OutsideValue_RedrawsThePad()
    {
        var cut = Render<NxSignature>();
        cut.Render(p => p.Add(x => x.Value, Png));

        var set = module.Invocations.Single(i => i.Identifier == "setValue");
        Assert.That(set.Arguments[1], Is.EqualTo(Png));
    }

    [Test]
    public void Options_ChangeSentOnce()
    {
        var cut = Render<NxSignature>();
        cut.Render(p => p.Add(x => x.StrokeWidth, 5));
        cut.Render(p => p.Add(x => x.StrokeWidth, 5));

        Assert.That(module.Invocations.Count(i => i.Identifier == "setOptions"), Is.EqualTo(1));
    }

    [Test]
    public void ReadOnlyAndDisabled_LockThePad_NoClear()
    {
        var cut = Render<NxSignature>(p => p.Add(x => x.Value, Png).Add(x => x.ReadOnly, true));
        Assert.That(cut.Find(".nx-signature").GetAttribute("data-locked"), Is.EqualTo("true"));
        Assert.That(cut.FindAll(".nx-signature-clear"), Is.Empty);

        cut.Render(p => p.Add(x => x.ReadOnly, false).Add(x => x.Disabled, true));
        Assert.That(cut.Find(".nx-signature").ClassList, Does.Contain("nx-signature-disabled"));
        Assert.That(cut.FindAll(".nx-signature-clear"), Is.Empty);

        cut.Render(p => p.Add(x => x.Disabled, false));
        Assert.That(cut.Find(".nx-signature").HasAttribute("data-locked"), Is.False);
    }

    [Test]
    public async Task Validation_InvalidMarksThePad()
    {
        var model = new Form();
        var context = new EditContext(model);
        var store = new ValidationMessageStore(context);
        var cut = Render<CascadingValue<EditContext>>(p => p
            .Add(x => x.Value, context)
            .Add(x => x.IsFixed, true)
            .AddChildContent<NxSignature>(s => s
                .Add(x => x.Value, model.Signature)
                .Add(x => x.ValueExpression, () => model.Signature)));

        await cut.InvokeAsync(() =>
        {
            store.Add(new FieldIdentifier(model, nameof(Form.Signature)), "Sign it");
            context.NotifyValidationStateChanged();
        });

        Assert.That(cut.Find(".nx-signature").ClassList, Does.Contain("nx-signature-invalid"));
    }

    private sealed class Form
    {
        [Required]
        public string? Signature { get; set; }
    }
}
