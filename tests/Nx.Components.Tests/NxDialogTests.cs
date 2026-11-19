using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace Nx.Components.Tests;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class NxDialogTests : BunitContext
{
    private NxDialogService service = default!;

    [SetUp]
    public void SetUp()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupModule("./_content/Nx.Components/nx-window.js").Mode = JSRuntimeMode.Loose;
        JSInterop.SetupModule("./_content/Nx.Components/nx-input.js").Mode = JSRuntimeMode.Loose;
        Services.AddNxDialogs();
        service = Services.GetRequiredService<NxDialogService>();
    }

    private IRenderedComponent<NxDialogHost> RenderHost() => Render<NxDialogHost>();

    /// <summary>Opens a dialog from the test (the service is safe from any thread) and waits for the host to show it.</summary>
    private static T Start<T>(IRenderedComponent<NxDialogHost> cut, Func<T> open)
    {
        var before = cut.FindAll(".nx-dialog").Count;
        var result = open();
        cut.WaitForState(() => cut.FindAll(".nx-dialog").Count > before);
        return result;
    }

    // ── NxDialog ──────────────────────────────────────────────────────────────

    [Test]
    public void Dialog_CompactWindowWithTitleContentButtons()
    {
        var cut = Render<NxDialog>(p => p
            .Add(x => x.Visible, true)
            .Add(x => x.Title, "Submit")
            .Add(x => x.Id, "d")
            .AddChildContent("<p class=\"body\">Body</p>")
            .Add(x => x.Buttons, (RenderFragment)(b => b.AddMarkupContent(0, "<button class=\"ok\">OK</button>"))));

        var window = cut.Find("#d");
        Assert.That(window.ClassList, Does.Contain("nx-window").And.Contain("nx-dialog").And.Contain("nx-window-compact").And.Contain("nx-window-small"));
        Assert.That(window.GetAttribute("role"), Is.EqualTo("dialog"));
        Assert.That(window.GetAttribute("aria-modal"), Is.EqualTo("true"));
        Assert.That(cut.Find(".nx-window-title").TextContent, Is.EqualTo("Submit"));
        Assert.That(cut.Find(".nx-window-body p.body").TextContent, Is.EqualTo("Body"));
        Assert.That(cut.Find(".nx-window-footer button.ok"), Is.Not.Null);
        Assert.That(window.ClassList, Does.Not.Contain("nx-window-draggable"));
    }

    [Test]
    public void Dialog_Hidden_RendersNothing_FullScreenOnPhoneDropsCompact()
    {
        var hidden = Render<NxDialog>(p => p.Add(x => x.Visible, false).Add(x => x.Title, "x"));
        Assert.That(hidden.FindAll(".nx-window"), Is.Empty);

        var full = Render<NxDialog>(p => p.Add(x => x.Visible, true).Add(x => x.FullScreenOnPhone, true).Add(x => x.Width, "700px"));
        Assert.That(full.Find(".nx-window").ClassList, Does.Not.Contain("nx-window-compact").And.Not.Contain("nx-window-small"));
    }

    [Test]
    public async Task Dialog_CloseButton_RaisesVisibleChanged()
    {
        var visible = true;
        var cut = Render<NxDialog>(p => p
            .Add(x => x.Visible, visible)
            .Add(x => x.Title, "x")
            .Add(x => x.VisibleChanged, v => visible = v));

        await cut.Find(".nx-window-close").ClickAsync(new MouseEventArgs());

        Assert.That(visible, Is.False);
    }

    // ── Service ───────────────────────────────────────────────────────────────

    [Test]
    public void Service_WithoutHost_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => service.Open(new NxDialogOptions()));
        Assert.That(ex!.Message, Does.Contain("NxDialogHost"));
    }

    [Test]
    public async Task Alert_TextMessage_OkCompletes()
    {
        var cut = RenderHost();
        var task = Start(cut, () => service.AlertAsync("Line one\n<b>not bold</b>", "Saved"));

        var dialog = cut.Find(".nx-dialog");
        Assert.That(cut.Find(".nx-window-title").TextContent, Is.EqualTo("Saved"));
        var message = cut.Find(".nx-dialog-message");
        Assert.That(message.TextContent, Is.EqualTo("Line one\n<b>not bold</b>"));
        Assert.That(message.QuerySelector("b"), Is.Null, "markup is shown as text");
        Assert.That(dialog.GetAttribute("aria-describedby"), Is.EqualTo(message.Id));
        var ok = cut.Find($"#{dialog.Id}-confirm");
        Assert.That(ok.TextContent.Trim(), Is.EqualTo("OK"));
        Assert.That(ok.HasAttribute("autofocus"), Is.True);
        Assert.That(cut.FindAll($"#{dialog.Id}-cancel"), Is.Empty);
        Assert.That(task.IsCompleted, Is.False);

        await ok.ClickAsync(new MouseEventArgs());

        Assert.That(task.IsCompleted, Is.True);
        Assert.That(cut.FindAll(".nx-dialog"), Is.Empty);
    }

    [Test]
    public async Task Confirm_TrueFalseAndDismiss()
    {
        var cut = RenderHost();

        var yes = Start(cut, () => service.ConfirmAsync("Delete?", "Delete", confirmText: "Delete", confirmVariant: NxButtonVariant.Danger));
        var confirm = cut.Find(".nx-dialog [id$=-confirm]");
        Assert.That(confirm.TextContent.Trim(), Is.EqualTo("Delete"));
        Assert.That(confirm.ClassList, Does.Contain("nx-button-danger"));
        await confirm.ClickAsync(new MouseEventArgs());
        Assert.That(await yes, Is.True);

        var no = Start(cut, () => service.ConfirmAsync("Delete?", cancelText: "Keep"));
        var cancel = cut.Find(".nx-dialog [id$=-cancel]");
        Assert.That(cancel.TextContent.Trim(), Is.EqualTo("Keep"));
        await cancel.ClickAsync(new MouseEventArgs());
        Assert.That(await no, Is.False);

        var dismissed = Start(cut, () => service.ConfirmAsync("Delete?"));
        await cut.Find(".nx-window-close").ClickAsync(new MouseEventArgs());
        Assert.That(await dismissed, Is.False);
    }

    [Test]
    public async Task Prompt_ReturnsTypedText_EnterConfirms_CancelNull()
    {
        var cut = RenderHost();

        var name = Start(cut, () => service.PromptAsync("Name of the draft", "New draft", value: "Draft", placeholder: "Name"));
        var input = cut.Find(".nx-dialog input");
        Assert.That(input.GetAttribute("value"), Is.EqualTo("Draft"));
        Assert.That(input.GetAttribute("placeholder"), Is.EqualTo("Name"));
        Assert.That(cut.Find(".nx-dialog [id$=-confirm]").HasAttribute("autofocus"), Is.False);
        await input.InputAsync(new ChangeEventArgs { Value = "Spring plan" });
        await cut.Find(".nx-dialog-content").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });
        Assert.That(await name, Is.EqualTo("Spring plan"));

        var cancelled = Start(cut, () => service.PromptAsync("Name"));
        await cut.Find(".nx-dialog [id$=-cancel]").ClickAsync(new MouseEventArgs());
        Assert.That(await cancelled, Is.Null);
    }

    [Test]
    public async Task Show_ContentFooterAndChecksInOrder()
    {
        var cut = RenderHost();
        var steps = new List<string>();
        var validateMessage = "Give a name.";
        var options = new NxDialogOptions
        {
            Id = "crew",
            Title = "New crew",
            ConfirmText = "Create",
            ShowCancelButton = true,
            Content = b => b.AddMarkupContent(0, "<input class=\"name\" />"),
            Footer = b => b.AddMarkupContent(0, "<span class=\"extra\">Extra</span>"),
            ValidateAsync = () => { steps.Add("validateAsync"); return Task.FromResult(true); },
            Validate = () => { steps.Add("validate"); return validateMessage; },
            OnConfirmAsync = () => { steps.Add("confirm"); return Task.FromResult(true); }
        };
        var result = Start(cut, () => service.ShowAsync(options));

        Assert.That(cut.Find("#crew .nx-dialog-content input.name"), Is.Not.Null);
        Assert.That(cut.Find("#crew .nx-dialog-footer-start .extra"), Is.Not.Null);
        Assert.That(cut.Find("#crew-confirm").HasAttribute("autofocus"), Is.False, "content takes the first focus");

        await cut.Find("#crew-confirm").ClickAsync(new MouseEventArgs());
        Assert.That(cut.Find("#crew .nx-dialog-error").TextContent, Is.EqualTo("Give a name."));
        Assert.That(cut.Find("#crew .nx-dialog-error").GetAttribute("role"), Is.EqualTo("alert"));
        Assert.That(result.IsCompleted, Is.False);

        validateMessage = "";
        await cut.Find("#crew-confirm").ClickAsync(new MouseEventArgs());
        Assert.That(await result, Is.True);
        Assert.That(steps, Is.EqualTo(new[] { "validateAsync", "validate", "validateAsync", "validate", "confirm" }));
    }

    [Test]
    public async Task Show_OnConfirmFalse_KeepsItOpen_ValidateAsyncFalseStopsFirst()
    {
        var cut = RenderHost();
        var valid = false;
        var attempts = 0;
        var result = Start(cut, () => service.ShowAsync(new NxDialogOptions
        {
            Id = "save",
            ValidateAsync = () => Task.FromResult(valid),
            OnConfirmAsync = () => Task.FromResult(++attempts > 1)
        }));

        await cut.Find("#save-confirm").ClickAsync(new MouseEventArgs());
        Assert.That(attempts, Is.EqualTo(0), "ValidateAsync false stops before OnConfirmAsync");
        valid = true;
        await cut.Find("#save-confirm").ClickAsync(new MouseEventArgs());
        Assert.That(result.IsCompleted, Is.False, "OnConfirmAsync false keeps it open");
        await cut.Find("#save-confirm").ClickAsync(new MouseEventArgs());
        Assert.That(await result, Is.True);
    }

    [Test]
    public async Task Show_OptionsReachTheWindow()
    {
        var cut = RenderHost();
        Start(cut, () => service.Open(new NxDialogOptions
        {
            Id = "big",
            Width = "800px",
            MaxHeight = "60vh",
            Closable = false,
            Modal = false,
            Resizable = true,
            Maximizable = true,
            FullScreenOnPhone = true,
            FillContent = true,
            ShowConfirmButton = false,
            Class = "mine"
        }));

        var window = cut.Find("#big");
        Assert.That(window.ClassList, Does.Contain("mine").And.Contain("nx-window-resizable").And.Not.Contain("nx-window-compact").And.Not.Contain("nx-window-modal"));
        Assert.That(cut.FindAll("#big .nx-window-close"), Is.Empty);
        Assert.That(cut.FindAll("#big .nx-window-maximize"), Has.Count.EqualTo(1));
        Assert.That(cut.FindAll("#big .nx-window-footer"), Is.Empty);
        var content = cut.Find("#big .nx-dialog-content");
        Assert.That(content.ClassList, Does.Contain("nx-dialog-content-fill"));
        Assert.That(content.GetAttribute("style"), Is.EqualTo("max-height: 60vh; overflow: auto"));
    }

    [Test]
    public async Task Open_CloseFromCode_AndFromCascadedContent()
    {
        var cut = RenderHost();
        var first = Start(cut, () => service.Open(new NxDialogOptions { Message = "a" }));
        Assert.That(service.OpenDialogs, Has.Count.EqualTo(1));

        first.Close(true);
        Assert.That(await first.Result, Is.True);
        cut.WaitForState(() => cut.FindAll(".nx-dialog").Count == 0);

        var second = Start(cut, () => service.Open(new NxDialogOptions
        {
            Content = b =>
            {
                b.OpenComponent<CloseFromInside>(0);
                b.CloseComponent();
            }
        }));
        await cut.Find("button.inside").ClickAsync(new MouseEventArgs());
        Assert.That(await second.Result, Is.False);
    }

    /// <summary>Content that closes its dialog through the cascaded reference.</summary>
    private sealed class CloseFromInside : ComponentBase
    {
        [CascadingParameter] private NxDialogReference Dialog { get; set; } = default!;

        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "button");
            builder.AddAttribute(1, "class", "inside");
            builder.AddAttribute(2, "onclick", EventCallback.Factory.Create(this, () => Dialog.Close(false)));
            builder.CloseElement();
        }
    }

    [Test]
    public async Task Stacked_TwoAtOnce()
    {
        var cut = RenderHost();
        Start(cut, () => service.Open(new NxDialogOptions { Id = "one", Message = "1" }));
        Start(cut, () => service.Open(new NxDialogOptions { Id = "two", Message = "2" }));
        Assert.That(cut.FindAll(".nx-dialog").Select(d => d.Id), Is.EqualTo(new[] { "one", "two" }));
    }

    [Test]
    public async Task Progress_ShowsOnFirstReport_ClosesOnDispose()
    {
        var cut = RenderHost();
        var progress = service.CreateProgress("Importing");
        await cut.InvokeAsync(() => { });
        Assert.That(cut.FindAll(".nx-dialog-progress"), Is.Empty);

        progress.Report("File 1 of 3");
        cut.WaitForState(() => cut.FindAll(".nx-dialog-progress").Count == 1);
        Assert.That(cut.Find(".nx-dialog-progress .nx-window-title").TextContent, Is.EqualTo("Importing"));
        Assert.That(cut.Find(".nx-dialog-progress-text").TextContent, Is.EqualTo("File 1 of 3"));
        Assert.That(cut.FindAll(".nx-dialog-progress .nx-window-close"), Is.Empty);

        progress.Report("File 2 of 3");
        cut.WaitForState(() => cut.Find(".nx-dialog-progress-text").TextContent == "File 2 of 3");

        progress.Dispose();
        cut.WaitForState(() => cut.FindAll(".nx-dialog-progress").Count == 0);
        progress.Report("ignored");
        Assert.That(progress.IsClosed, Is.True);
    }

    // ── With an NxWindowHost ──────────────────────────────────────────────────

    [Test]
    public async Task WindowHost_TakesNxDialog_ButNotInlineNorTheServicesDialogs()
    {
        var cut = Render(b =>
        {
            b.OpenElement(0, "div");
            b.AddAttribute(1, "id", "page");
            b.OpenComponent<NxDialog>(2);
            b.AddComponentParameter(3, "Visible", true);
            b.AddComponentParameter(4, "Id", "hosted");
            b.CloseComponent();
            b.OpenComponent<NxDialog>(5);
            b.AddComponentParameter(6, "Visible", true);
            b.AddComponentParameter(7, "Id", "inline");
            b.AddComponentParameter(8, "Inline", true);
            b.CloseComponent();
            b.CloseElement();
            b.OpenElement(9, "div");
            b.AddAttribute(10, "id", "dialogs");
            b.OpenComponent<NxDialogHost>(11);
            b.CloseComponent();
            b.CloseElement();
            b.OpenElement(12, "div");
            b.AddAttribute(13, "id", "windows");
            b.OpenComponent<NxWindowHost>(14);
            b.CloseComponent();
            b.CloseElement();
        });

        Assert.That(cut.FindAll("#windows #hosted.nx-dialog"), Has.Count.EqualTo(1));
        Assert.That(cut.FindAll("#page #inline.nx-dialog"), Has.Count.EqualTo(1));

        // The dialog host keeps its own dialogs, so their content still gets the NxDialogReference it cascades.
        var dialog = service.Open(new NxDialogOptions
        {
            Id = "from-service",
            Content = b =>
            {
                b.OpenComponent<CloseFromInside>(0);
                b.CloseComponent();
            }
        });
        cut.WaitForState(() => cut.FindAll("#from-service").Count == 1);
        Assert.That(cut.FindAll("#dialogs #from-service"), Has.Count.EqualTo(1));
        await cut.Find("#from-service button.inside").ClickAsync(new MouseEventArgs());
        Assert.That(await dialog.Result, Is.False);
    }

    [Test]
    public async Task HostDispose_DismissesOpenDialogs()
    {
        var cut = RenderHost();
        var dialog = Start(cut, () => service.Open(new NxDialogOptions { Message = "x" }));
        await DisposeComponentsAsync();
        Assert.That(await dialog.Result, Is.Null);
    }
}
