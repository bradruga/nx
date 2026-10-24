using Bunit;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;

namespace Nx.Components.Tests;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class NxFileSelectTests : BunitContext
{
    [SetUp]
    public void SetUp() => JSInterop.Mode = JSRuntimeMode.Loose;

    private static InputFileContent File(string name, int size = 10) => InputFileContent.CreateFromText(new string('x', size), name);

    [Test]
    public void Render_DropZoneWithInput_HintFromLimits()
    {
        var cut = Render<NxFileSelect>(p => p
            .Add(x => x.Id, "docs")
            .Add(x => x.AllowedExtensions, new[] { "pdf", "*.PNG" })
            .Add(x => x.MaxFileSize, 10L * 1024 * 1024));

        var input = cut.Find("input#docs[type=file]");
        Assert.That(input.GetAttribute("accept"), Is.EqualTo(".pdf,.png"));
        Assert.That(input.HasAttribute("multiple"), Is.True);
        Assert.That(input.GetAttribute("aria-label"), Is.EqualTo("Select files"));
        Assert.That(cut.Find(".nx-filezone-hint").TextContent, Is.EqualTo("PDF, PNG · up to 10 MB"));
        Assert.That(cut.FindAll(".nx-filerow"), Is.Empty);
    }

    [Test]
    public void Select_ListsFiles_ChecksLimits_RaisesOnSelect()
    {
        NxFileSelectEventArgs? args = null;
        var cut = Render<NxFileSelect>(p => p
            .Add(x => x.AllowedExtensions, new[] { ".pdf" })
            .Add(x => x.MaxFileSize, 100L)
            .Add(x => x.OnSelect, e => args = e));

        cut.FindComponent<InputFile>().UploadFiles(File("pour.pdf"), File("photo.png"), File("big.pdf", 200));

        Assert.That(args!.Files.Select(f => (f.Name, f.Error)), Is.EqualTo(new (string, NxFileError?)[]
        {
            ("pour.pdf", null), ("photo.png", NxFileError.InvalidExtension), ("big.pdf", NxFileError.TooLarge)
        }));
        var rows = cut.FindAll(".nx-filerow");
        Assert.That(rows, Has.Count.EqualTo(3));
        Assert.That(rows[0].QuerySelector(".nx-filerow-name")!.TextContent, Is.EqualTo("pour.pdf"));
        Assert.That(rows[1].QuerySelector(".nx-filerow-error")!.TextContent, Is.EqualTo("This file type is not allowed."));
        Assert.That(rows[2].ClassList, Does.Contain("nx-filerow-error-row"));
        Assert.That(cut.Instance.Files.Count(f => f.IsValid), Is.EqualTo(1));
    }

    [Test]
    public void Select_Cancelled_NotListed()
    {
        var cut = Render<NxFileSelect>(p => p.Add(x => x.OnSelect, e => e.IsCancelled = true));
        cut.FindComponent<InputFile>().UploadFiles(File("a.pdf"));
        Assert.That(cut.FindAll(".nx-filerow"), Is.Empty);
    }

    [Test]
    public void Single_ReplacesTheFile()
    {
        var cut = Render<NxFileSelect>(p => p.Add(x => x.Multiple, false));
        Assert.That(cut.Find("input").HasAttribute("multiple"), Is.False);

        cut.FindComponent<InputFile>().UploadFiles(File("a.pdf"));
        cut.FindComponent<InputFile>().UploadFiles(File("b.pdf"));

        Assert.That(cut.Instance.Files.Select(f => f.Name), Is.EqualTo(new[] { "b.pdf" }));
    }

    [Test]
    public async Task Remove_RaisesOnRemove_ReadsTheStream()
    {
        NxFileSelectEventArgs? removed = null;
        var cut = Render<NxFileSelect>(p => p.Add(x => x.OnRemove, e => removed = e));
        cut.FindComponent<InputFile>().UploadFiles(File("a.txt", 5), File("b.txt"));

        using (var reader = new StreamReader(cut.Instance.Files[0].OpenReadStream()))
            Assert.That(await reader.ReadToEndAsync(), Is.EqualTo("xxxxx"));

        await cut.Find("button[aria-label='Remove a.txt']").ClickAsync(new MouseEventArgs());

        Assert.That(removed!.Files.Single().Name, Is.EqualTo("a.txt"));
        Assert.That(cut.FindAll(".nx-filerow"), Has.Count.EqualTo(1));
    }

    [Test]
    public void Disabled_InputAndRemoveOff()
    {
        var cut = Render<NxFileSelect>();
        cut.FindComponent<InputFile>().UploadFiles(File("a.pdf"));
        cut.Render(p => p.Add(x => x.Disabled, true));

        Assert.That(cut.Find("input").HasAttribute("disabled"), Is.True);
        Assert.That(cut.Find(".nx-fileselect-remove").HasAttribute("disabled"), Is.True);
        Assert.That(cut.Find(".nx-filezone").ClassList, Does.Contain("nx-filezone-disabled"));
    }

    [Test]
    public void ClearFiles_EmptiesTheList()
    {
        var cut = Render<NxFileSelect>();
        cut.FindComponent<InputFile>().UploadFiles(File("a.pdf"));
        cut.InvokeAsync(cut.Instance.ClearFiles);
        Assert.That(cut.FindAll(".nx-filerow"), Is.Empty);
    }

    [TestCase(512, "512 B")]
    [TestCase(14 * 1024, "14 KB")]
    [TestCase(2_516_582, "2.4 MB")]
    public void Size_ForPeople(long bytes, string expected) => Assert.That(FileRules.Size(bytes), Is.EqualTo(expected));
}
