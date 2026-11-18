using Bunit;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;

namespace Nx.Components.Tests;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class NxUploadTests : BunitContext
{
    private BunitJSModuleInterop module = default!;

    [SetUp]
    public void SetUp()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        module = JSInterop.SetupModule("./_content/Nx.Components/nx-upload.js");
        module.Mode = JSRuntimeMode.Loose;
    }

    private static NxUpload.PickedFile Pick(string id, string name, long size = 10) => new(id, name, size);

    private IEnumerable<JSRuntimeInvocation> Calls(string identifier) => module.Invocations.Where(i => i.Identifier == identifier);

    [Test]
    public void Render_InitsJs_WithTheInputAndZone()
    {
        var cut = Render<NxUpload>(p => p.Add(x => x.SaveUrl, "api/files").Add(x => x.Id, "att"));

        Assert.That(cut.Find("input#att[type=file]").GetAttribute("aria-label"), Is.EqualTo("Select files to upload"));
        Assert.That(Calls("init").Count(), Is.EqualTo(1));
        Assert.That(cut.FindAll("#att-paste"), Is.Empty);
    }

    [Test]
    public async Task Picked_ValidSent_InvalidListedNotSent()
    {
        var cut = Render<NxUpload>(p => p
            .Add(x => x.SaveUrl, "api/files")
            .Add(x => x.AllowedExtensions, new[] { ".pdf" })
            .Add(x => x.MaxConcurrentUploads, 2));

        await cut.Instance.OnFilesAdded([Pick("1", "pour.pdf"), Pick("2", "notes.exe")]);

        var upload = Calls("upload").Single();
        Assert.That(upload.Arguments[1], Is.EqualTo(new[] { "1" }));
        Assert.That(upload.Arguments[2], Is.EqualTo("api/files"));
        Assert.That(upload.Arguments[3], Is.EqualTo("files"));
        Assert.That(upload.Arguments[4], Is.EqualTo(true));
        Assert.That(upload.Arguments[5], Is.EqualTo(2));
        Assert.That(Calls("forget").Single().Arguments[1], Is.EqualTo("2"));
        Assert.That(cut.Instance.Files.Select(f => f.Status), Is.EqualTo(new[] { NxUploadStatus.Uploading, NxUploadStatus.Invalid }));
        Assert.That(cut.FindAll(".nx-filerow-error")[0].TextContent, Is.EqualTo("This file type is not allowed."));
    }

    [Test]
    public async Task Progress_ThenSuccess_RaisesWithTheServerAnswer()
    {
        NxUploadEventArgs? success = null;
        var cut = Render<NxUpload>(p => p.Add(x => x.SaveUrl, "api/files").Add(x => x.OnSuccess, e => success = e));
        await cut.Instance.OnFilesAdded([Pick("1", "pour.pdf")]);

        await cut.Instance.OnUploadProgress("1", 40);
        Assert.That(cut.Find(".nx-filerow-status").TextContent, Does.Contain("40%"));
        Assert.That(cut.Find(".nx-progressbar").GetAttribute("aria-valuenow"), Is.EqualTo("40"));

        await cut.Instance.OnUploadDone("1", 200, "tmp-123");

        Assert.That(success!.File.ResponseText, Is.EqualTo("tmp-123"));
        Assert.That(success.File.Status, Is.EqualTo(NxUploadStatus.Uploaded));
        Assert.That(cut.Find(".nx-filerow").ClassList, Does.Contain("nx-filerow-success"));
        Assert.That(cut.FindAll(".nx-progressbar"), Is.Empty);
    }

    [Test]
    public async Task Failure_RaisesOnError_RetrySendsAgain()
    {
        NxUploadEventArgs? error = null;
        var cut = Render<NxUpload>(p => p.Add(x => x.SaveUrl, "api/files").Add(x => x.OnError, e => error = e));
        await cut.Instance.OnFilesAdded([Pick("1", "fail.pdf")]);

        await cut.Instance.OnUploadDone("1", 500, "");

        Assert.That(error!.File.StatusCode, Is.EqualTo(500));
        Assert.That(cut.Find(".nx-filerow-error").TextContent, Is.EqualTo("Upload failed (500)."));
        await cut.Find("button[aria-label='Retry fail.pdf']").ClickAsync(new MouseEventArgs());
        Assert.That(Calls("upload").Count(), Is.EqualTo(2));
        Assert.That(cut.Instance.Files[0].Status, Is.EqualTo(NxUploadStatus.Uploading));
    }

    [Test]
    public async Task Cancel_RaisesOnCancel_TellsJs_LateAnswerIgnored()
    {
        NxUploadEventArgs? cancelled = null;
        var cut = Render<NxUpload>(p => p.Add(x => x.SaveUrl, "api/files").Add(x => x.OnCancel, e => cancelled = e));
        await cut.Instance.OnFilesAdded([Pick("1", "pour.pdf")]);
        Assert.That(cut.FindAll("button[aria-label='Remove pour.pdf']"), Is.Empty);

        await cut.Find("button[aria-label='Cancel pour.pdf']").ClickAsync(new MouseEventArgs());
        await cut.Instance.OnUploadDone("1", 200, "late");

        Assert.That(cancelled!.File.Status, Is.EqualTo(NxUploadStatus.Cancelled));
        Assert.That(Calls("cancel").Single().Arguments[1], Is.EqualTo("1"));
        Assert.That(cut.Find(".nx-filerow-status").TextContent, Does.Contain("Cancelled"));
    }

    [Test]
    public async Task Remove_RaisesOnRemove_ForgetsTheFile()
    {
        NxUploadEventArgs? removed = null;
        var cut = Render<NxUpload>(p => p.Add(x => x.SaveUrl, "api/files").Add(x => x.OnRemove, e => removed = e));
        await cut.Instance.OnFilesAdded([Pick("1", "pour.pdf")]);
        await cut.Instance.OnUploadDone("1", 200, "tmp-1");

        await cut.Find("button[aria-label='Remove pour.pdf']").ClickAsync(new MouseEventArgs());

        Assert.That(removed!.File.ResponseText, Is.EqualTo("tmp-1"));
        Assert.That(Calls("forget").Single().Arguments[1], Is.EqualTo("1"));
        Assert.That(cut.FindAll(".nx-filerow"), Is.Empty);
    }

    [Test]
    public async Task OnSelectCancelled_NothingListedOrSent()
    {
        var cut = Render<NxUpload>(p => p.Add(x => x.SaveUrl, "api/files").Add(x => x.OnSelect, e => e.IsCancelled = true));
        await cut.Instance.OnFilesAdded([Pick("1", "pour.pdf")]);

        Assert.That(cut.FindAll(".nx-filerow"), Is.Empty);
        Assert.That(Calls("upload"), Is.Empty);
        Assert.That(Calls("forget").Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task NoAutoUpload_UploadAndClearButtons()
    {
        var cut = Render<NxUpload>(p => p.Add(x => x.SaveUrl, "api/files").Add(x => x.Id, "u").Add(x => x.AutoUpload, false));
        await cut.Instance.OnFilesAdded([Pick("1", "a.pdf"), Pick("2", "b.pdf")]);
        Assert.That(Calls("upload"), Is.Empty);
        Assert.That(cut.Find(".nx-filerow-status").TextContent, Does.Contain("Waiting"));

        await cut.Find("#u-upload").ClickAsync(new MouseEventArgs());

        Assert.That(Calls("upload").Single().Arguments[1], Is.EqualTo(new[] { "1", "2" }));
        Assert.That(cut.FindAll("#u-upload"), Is.Empty);
    }

    [Test]
    public async Task Single_ReplacesTheOldFile()
    {
        var cut = Render<NxUpload>(p => p.Add(x => x.SaveUrl, "api/files").Add(x => x.Multiple, false));
        Assert.That(cut.Find("input").HasAttribute("multiple"), Is.False);

        await cut.Instance.OnFilesAdded([Pick("1", "a.pdf"), Pick("2", "b.pdf")]);
        await cut.Instance.OnUploadDone("1", 200, "x");
        await cut.Instance.OnFilesAdded([Pick("3", "c.pdf")]);

        Assert.That(cut.Instance.Files.Select(f => f.Name), Is.EqualTo(new[] { "c.pdf" }));
    }

    [Test]
    public async Task PasteButton_AsksJs_SaysWhenEmpty()
    {
        module.Setup<bool>("paste", _ => true).SetResult(false);
        var cut = Render<NxUpload>(p => p.Add(x => x.SaveUrl, "api/files").Add(x => x.Id, "u").Add(x => x.ShowPasteButton, true));

        await cut.Find("#u-paste").ClickAsync(new MouseEventArgs());

        Assert.That(cut.Find(".nx-upload-paste-message").TextContent, Is.EqualTo("Nothing to paste."));
    }

    [Test]
    public async Task NoAutoUpload_Clear_EmptiesWithoutOnRemove()
    {
        var removed = 0;
        var cut = Render<NxUpload>(p => p
            .Add(x => x.SaveUrl, "api/files").Add(x => x.Id, "u").Add(x => x.AutoUpload, false)
            .Add(x => x.OnRemove, _ => removed++));
        await cut.Instance.OnFilesAdded([Pick("1", "a.pdf"), Pick("2", "b.pdf")]);

        await cut.Find("#u-clear").ClickAsync(new MouseEventArgs());

        Assert.That(cut.FindAll(".nx-filerow"), Is.Empty);
        Assert.That(Calls("forget").Select(c => c.Arguments[1]), Is.EqualTo(new[] { "1", "2" }));
        Assert.That(Calls("upload"), Is.Empty);
        Assert.That(removed, Is.Zero);
    }

    [Test]
    public async Task NoAnswer_FailureSaysSo()
    {
        var cut = Render<NxUpload>(p => p.Add(x => x.SaveUrl, "api/files"));
        await cut.Instance.OnFilesAdded([Pick("1", "pour.pdf")]);

        await cut.Instance.OnUploadDone("1", 0, "");

        Assert.That(cut.Instance.Files[0].Status, Is.EqualTo(NxUploadStatus.Failed));
        Assert.That(cut.Find(".nx-filerow-error").TextContent, Is.EqualTo("Upload failed: no answer from the server."));
    }

    [Test]
    public async Task Cancelled_CanBeRetried()
    {
        var cut = Render<NxUpload>(p => p.Add(x => x.SaveUrl, "api/files"));
        await cut.Instance.OnFilesAdded([Pick("1", "pour.pdf")]);
        await cut.Find("button[aria-label='Cancel pour.pdf']").ClickAsync(new MouseEventArgs());

        await cut.Find("button[aria-label='Retry pour.pdf']").ClickAsync(new MouseEventArgs());

        Assert.That(Calls("upload").Count(), Is.EqualTo(2));
        Assert.That(cut.Instance.Files[0].Status, Is.EqualTo(NxUploadStatus.Uploading));
    }

    [Test]
    public async Task SaveFieldAndCredentials_SentToJs()
    {
        var cut = Render<NxUpload>(p => p
            .Add(x => x.SaveUrl, "api/photos")
            .Add(x => x.SaveField, "photo")
            .Add(x => x.WithCredentials, false));

        await cut.Instance.OnFilesAdded([Pick("1", "site.jpg")]);

        var upload = Calls("upload").Single();
        Assert.That(upload.Arguments[2], Is.EqualTo("api/photos"));
        Assert.That(upload.Arguments[3], Is.EqualTo("photo"));
        Assert.That(upload.Arguments[4], Is.EqualTo(false));
        Assert.That(upload.Arguments[5], Is.EqualTo(0));
    }

    [Test]
    public async Task SizeLimits_NotSent_MessageNamesTheLimit()
    {
        var cut = Render<NxUpload>(p => p.Add(x => x.SaveUrl, "api/files").Add(x => x.MaxFileSize, 1024L).Add(x => x.MinFileSize, 10L));

        await cut.Instance.OnFilesAdded([Pick("1", "big.pdf", 5000), Pick("2", "empty.pdf", 0)]);

        Assert.That(Calls("upload"), Is.Empty);
        Assert.That(cut.Instance.Files.Select(f => f.Status), Is.EqualTo(new[] { NxUploadStatus.Invalid, NxUploadStatus.Invalid }));
        Assert.That(cut.FindAll(".nx-filerow-error").Select(e => e.TextContent),
            Is.EqualTo(new[] { "Larger than the 1 KB limit.", "Smaller than the 10 B minimum." }));
        Assert.That(cut.Find(".nx-filezone-hint").TextContent, Is.EqualTo("up to 1 KB"));
    }

    [Test]
    public async Task Single_NewPick_RaisesOnRemoveForTheOldFile()
    {
        var removed = new List<string>();
        var cut = Render<NxUpload>(p => p
            .Add(x => x.SaveUrl, "api/files").Add(x => x.Multiple, false)
            .Add(x => x.OnRemove, e => removed.Add(e.File.Name)));
        await cut.Instance.OnFilesAdded([Pick("1", "a.pdf")]);
        await cut.Instance.OnUploadDone("1", 200, "tmp-a");

        await cut.Instance.OnFilesAdded([Pick("2", "b.pdf")]);

        Assert.That(removed, Is.EqualTo(new[] { "a.pdf" }));
    }

    [Test]
    public async Task ProgressAndAnswer_ForUnknownOrFinishedFile_Ignored()
    {
        var success = 0;
        var cut = Render<NxUpload>(p => p.Add(x => x.SaveUrl, "api/files").Add(x => x.OnSuccess, _ => success++));
        await cut.Instance.OnFilesAdded([Pick("1", "a.pdf")]);
        await cut.Instance.OnUploadDone("1", 200, "ok");

        await cut.Instance.OnUploadDone("1", 500, "late");
        await cut.Instance.OnUploadDone("nope", 200, "");

        Assert.That(success, Is.EqualTo(1));
        Assert.That(cut.Instance.Files[0].Status, Is.EqualTo(NxUploadStatus.Uploaded));
    }

    [Test]
    public async Task Disabled_IgnoresPicks()
    {
        var cut = Render<NxUpload>(p => p.Add(x => x.SaveUrl, "api/files").Add(x => x.Disabled, true));
        await cut.Instance.OnFilesAdded([Pick("1", "a.pdf")]);
        Assert.That(cut.Find("input").HasAttribute("disabled"), Is.True);
        Assert.That(cut.FindAll(".nx-filerow"), Is.Empty);
    }
}
