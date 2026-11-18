using System.Text;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace Nx.Playwright;

/// <summary>NxFileSelect and NxUpload, against the demo's <c>/demo/upload</c> endpoint.</summary>
[TestFixtureSource(typeof(TestConfig), nameof(TestConfig.AppUrls))]
public class FileUploadTests : PageTest
{
    private readonly string _baseUrl;

    public FileUploadTests(string baseUrl) => _baseUrl = baseUrl;

    private async Task GoToPage(string path)
    {
        await Page.GotoAsync(_baseUrl + path);
        await Expect(Page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();
    }

    private static FilePayload File(string name, int size = 64, string mime = "application/octet-stream") =>
        new() { Name = name, MimeType = mime, Buffer = Encoding.ASCII.GetBytes(new string('x', size)) };

    /// <summary>The file rows of the NxFileSelect or NxUpload whose input has this id.</summary>
    private ILocator Rows(string id) =>
        Page.Locator(".nx-fileselect, .nx-upload").Filter(new() { Has = Page.Locator($"#{id}") }).Locator(".nx-filerow");

    // ── NxFileSelect ──────────────────────────────────────────────────────────

    [Test]
    public async Task Select_ReadsValidFiles_ListsRefused()
    {
        await GoToPage("/fileselect/overview");

        await Page.Locator("#overview-files").SetInputFilesAsync([File("takeoff.xlsx", 1500), File("virus.exe")]);

        await Expect(Page.Locator("#overview-read")).ToHaveTextAsync("Read 1 file(s), 1,500 bytes");
        var rows = Rows("overview-files");
        await Expect(rows).ToHaveCountAsync(2);
        await Expect(rows.Nth(0)).ToContainTextAsync("takeoff.xlsx");
        await Expect(rows.Nth(1).Locator(".nx-filerow-error")).ToHaveTextAsync("This file type is not allowed.");

        await Page.GetByRole(AriaRole.Button, new() { Name = "Remove virus.exe" }).ClickAsync();
        await Expect(rows).ToHaveCountAsync(1);
        await Expect(Page.Locator("#overview-read")).ToHaveTextAsync("Removed virus.exe");
    }

    [Test]
    public async Task Select_SizeChecks_AndTheZoneTakesFocus()
    {
        await GoToPage("/fileselect/examples");

        await Page.Locator("#checks-files").SetInputFilesAsync([File("ok.png", 100), File("tiny.png", 2), File("huge.jpg", 300_000)]);
        await Expect(Page.Locator("#checks-result")).ToHaveTextAsync("1 accepted, 2 refused");
        await Expect(Rows("checks-files").Locator(".nx-filerow-error")).ToHaveTextAsync(["Smaller than the 10 B minimum.", "Larger than the 195 KB limit."]);

        await Page.Locator("#checks-files").FocusAsync();
        var outline = await Page.Locator("#checks-files").EvaluateAsync<string>("e => getComputedStyle(e.closest('.nx-filezone')).outlineStyle");
        Assert.That(outline, Is.EqualTo("solid"), "the zone shows the input's focus");
    }

    [Test]
    public async Task Select_Single_ReplacesAndPreviews()
    {
        await GoToPage("/fileselect/examples");

        var input = Page.Locator("#single-file");
        await input.SetInputFilesAsync(File("a.csv", 10));
        await Expect(Rows("single-file")).ToContainTextAsync("a.csv");
        await input.SetInputFilesAsync(new FilePayload { Name = "b.csv", MimeType = "text/csv", Buffer = Encoding.UTF8.GetBytes("pour,yards\nP-1,14") });

        await Expect(Rows("single-file")).ToHaveCountAsync(1);
        await Expect(Rows("single-file")).ToContainTextAsync("b.csv");
        await Expect(Page.Locator("#single-preview")).ToContainTextAsync("P-1,14");
    }

    [Test]
    public async Task Select_OnSelectRefuses_ClearEmpties()
    {
        await GoToPage("/fileselect/examples");

        var input = Page.Locator("#limit-files");
        await input.SetInputFilesAsync([File("1.txt"), File("2.txt")]);
        await input.SetInputFilesAsync([File("3.txt"), File("4.txt")]);
        await Expect(Page.Locator("#limit-result")).ToHaveTextAsync("Refused 2 file(s): the list holds 3.");
        await Expect(Rows("limit-files")).ToHaveCountAsync(2);

        await Page.Locator("#limit-clear").ClickAsync();
        await Expect(Rows("limit-files")).ToHaveCountAsync(0);
        await Expect(Page.Locator("#disabled-files")).ToBeDisabledAsync();
    }

    // ── NxUpload ──────────────────────────────────────────────────────────────

    [Test]
    public async Task Upload_SendsAndShowsTheServerAnswer_RemoveRaises()
    {
        await GoToPage("/upload/overview");

        await Page.Locator("#overview-upload").SetInputFilesAsync(File("pour.pdf", 2000));

        await Expect(Page.Locator("#overview-log")).ToHaveTextAsync(new System.Text.RegularExpressions.Regex("^pour.pdf stored as tmp-"));
        var row = Rows("overview-upload").First;
        await Expect(row).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("nx-filerow-success"));
        await Expect(row.Locator(".nx-filerow-status")).ToContainTextAsync("Uploaded");

        await Page.GetByRole(AriaRole.Button, new() { Name = "Remove pour.pdf" }).ClickAsync();
        await Expect(Page.Locator("#overview-log")).ToHaveTextAsync("Removed pour.pdf");
        await Expect(Rows("overview-upload")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Upload_ServerError_RetryAgain()
    {
        await GoToPage("/upload/examples");

        await Page.Locator("#results-upload").SetInputFilesAsync([File("fine.txt"), File("fail.txt")]);

        var log = Page.Locator("#results-log li");
        await Expect(log.Filter(new() { HasText = "Failed fail.txt: 500" })).ToHaveCountAsync(1);
        await Expect(log.Filter(new() { HasText = "OK fine.txt: tmp-" })).ToHaveCountAsync(1);
        var failed = Rows("results-upload").Filter(new() { HasText = "fail.txt" });
        await Expect(failed.Locator(".nx-filerow-error")).ToHaveTextAsync("Upload failed (500).");

        await Page.GetByRole(AriaRole.Button, new() { Name = "Retry fail.txt" }).ClickAsync();
        await Expect(log.Filter(new() { HasText = "Failed fail.txt: 500" })).ToHaveCountAsync(2);
    }

    [Test]
    public async Task Upload_OnDemand_WaitsForTheButton()
    {
        await GoToPage("/upload/examples");

        await Page.Locator("#manual-upload").SetInputFilesAsync([File("a.txt"), File("b.txt"), File("c.txt")]);
        var rows = Rows("manual-upload");
        await Expect(rows.Locator(".nx-filerow-status")).ToHaveTextAsync(["· Waiting", "· Waiting", "· Waiting"]);

        await Page.Locator("#manual-upload-upload").ClickAsync();
        await Expect(rows.Locator(".nx-filerow-status")).ToHaveTextAsync(["· Uploaded", "· Uploaded", "· Uploaded"]);
        await Expect(Page.Locator("#manual-upload-upload")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Upload_Single_ChecksNeverSent()
    {
        await GoToPage("/upload/examples");

        var requests = 0;
        Page.Request += (_, r) => { if (r.Url.Contains("/demo/upload")) requests++; };
        await Page.Locator("#single-upload").SetInputFilesAsync(File("drawing.dwg"));

        await Expect(Rows("single-upload").Locator(".nx-filerow-error")).ToHaveTextAsync("This file type is not allowed.");
        Assert.That(requests, Is.Zero);
        await Expect(Page.Locator("#single-upload")).Not.ToHaveAttributeAsync("multiple", "");
        await Expect(Page.Locator("#disabled-upload")).ToBeDisabledAsync();
    }

    [Test]
    public async Task Upload_CancelMidUpload_NoSuccess_RowSaysCancelled()
    {
        await GoToPage("/upload/examples");
        var release = new TaskCompletionSource();
        var arrived = new TaskCompletionSource();
        await Page.RouteAsync("**/demo/upload", async route =>
        {
            arrived.TrySetResult();
            await release.Task;
            try { await route.FulfillAsync(new() { Status = 200, Body = "tmp-held" }); }
            catch (PlaywrightException) { }  // aborted by the cancel
        });

        await Page.Locator("#results-upload").SetInputFilesAsync(File("slow.txt", 5000));
        await arrived.Task.WaitAsync(TimeSpan.FromSeconds(15));
        var row = Rows("results-upload").Filter(new() { HasText = "slow.txt" });
        await row.GetByRole(AriaRole.Button, new() { Name = "Cancel slow.txt" }).ClickAsync();

        await Expect(row.Locator(".nx-filerow-status")).ToHaveTextAsync("· Cancelled");
        await Expect(Page.Locator("#results-log li").First).ToHaveTextAsync("Cancelled slow.txt");
        release.SetResult();
        await Expect(row.GetByRole(AriaRole.Button, new() { Name = "Retry slow.txt" })).ToBeVisibleAsync();
        // The held answer is let go: an aborted request must not turn the row into a success.
        await Page.WaitForTimeoutAsync(1000);
        await Expect(row.Locator(".nx-filerow-status")).ToHaveTextAsync("· Cancelled");
        await Expect(row).Not.ToHaveClassAsync(new System.Text.RegularExpressions.Regex("nx-filerow-success"));
        await Expect(Page.Locator("#results-log li").Filter(new() { HasText = "OK slow.txt" })).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Upload_MaxConcurrentUploads_HoldsTheRestInTheQueue()
    {
        await GoToPage("/upload/examples");
        var gate = new object();
        int inFlight = 0, maxInFlight = 0, total = 0;
        var two = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        await Page.RouteAsync("**/demo/upload", async route =>
        {
            lock (gate)
            {
                inFlight++;
                total++;
                maxInFlight = Math.Max(maxInFlight, inFlight);
                if (total == 2) two.TrySetResult();
            }
            await release.Task;
            lock (gate) inFlight--;
            await route.FulfillAsync(new() { Status = 200, Body = "tmp-held" });
        });

        // The demo's on-demand upload sends at most two at once.
        await Page.Locator("#manual-upload").SetInputFilesAsync([File("a.txt"), File("b.txt"), File("c.txt"), File("d.txt"), File("e.txt")]);
        await Page.Locator("#manual-upload-upload").ClickAsync();
        var rows = Rows("manual-upload");
        await two.Task.WaitAsync(TimeSpan.FromSeconds(15));
        // Give a broken limit time to send a third.
        await Page.WaitForTimeoutAsync(1000);
        lock (gate) Assert.That(total, Is.EqualTo(2), "two requests sent, three queued");

        release.SetResult();
        await Expect(rows.Locator(".nx-filerow-status")).ToHaveTextAsync(["· Uploaded", "· Uploaded", "· Uploaded", "· Uploaded", "· Uploaded"]);
        lock (gate)
        {
            Assert.That(total, Is.EqualTo(5));
            Assert.That(maxInFlight, Is.EqualTo(2));
        }
    }

    [Test]
    public async Task Upload_DropOnTheZone_ListsAndSends()
    {
        await GoToPage("/upload/overview");
        var sent = new List<string>();
        Page.Request += (_, r) => { if (r.Url.Contains("/demo/upload") && r.Method == "POST") lock (sent) sent.Add(r.Url); };

        var input = Page.Locator("#overview-upload");
        var zone = Page.Locator(".nx-upload:has(#overview-upload) .nx-filezone");
        await zone.ScrollIntoViewIfNeededAsync();
        // A drop from the desktop: drag events carrying a DataTransfer with the files. The browser's own drop into the
        // file input (it lies over the zone) cannot be faked from script, so its effect - files set, change raised - is.
        await input.EvaluateAsync("""
            input => {
                const dt = new DataTransfer();
                dt.items.add(new File(['dropped one'], 'dropped-a.txt', { type: 'text/plain' }));
                dt.items.add(new File(['dropped two'], 'dropped-b.txt', { type: 'text/plain' }));
                window.__nxDrop = dt;
                input.dispatchEvent(new DragEvent('dragenter', { dataTransfer: dt, bubbles: true }));
            }
            """);
        await Expect(zone).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("nx-filezone-dragging"));
        await input.EvaluateAsync("""
            input => {
                const dt = window.__nxDrop;
                input.dispatchEvent(new DragEvent('drop', { dataTransfer: dt, bubbles: true }));
                input.files = dt.files;
                input.dispatchEvent(new Event('change', { bubbles: true }));
            }
            """);

        await Expect(zone).Not.ToHaveClassAsync(new System.Text.RegularExpressions.Regex("nx-filezone-dragging"));
        var rows = Rows("overview-upload");
        await Expect(rows.Locator(".nx-filerow-name")).ToHaveTextAsync(["dropped-a.txt", "dropped-b.txt"]);
        await Expect(rows.Locator(".nx-filerow-status")).ToHaveTextAsync(["· Uploaded", "· Uploaded"]);
        await Expect(Page.Locator("#overview-log")).ToHaveTextAsync(new System.Text.RegularExpressions.Regex("^dropped-[ab].txt stored as tmp-"));
        lock (sent) Assert.That(sent, Has.Count.EqualTo(2));
    }

    [TestCase(390)]
    [TestCase(1280)]
    public async Task Upload_LongNamesStayInTheRow(int width)
    {
        await Page.SetViewportSizeAsync(width, 800);
        await GoToPage("/upload/examples");

        await Page.Locator("#manual-upload").SetInputFilesAsync(File("Precast-yard-north-wall-panel-schedule-revision-14-final-final.pdf"));
        var row = Rows("manual-upload").First;
        await Expect(row).ToBeVisibleAsync();
        var overflow = await row.EvaluateAsync<bool>("e => e.scrollWidth > e.clientWidth + 1");
        Assert.That(overflow, Is.False);
        var docWidth = await Page.EvaluateAsync<int>("() => document.documentElement.scrollWidth");
        Assert.That(docWidth, Is.LessThanOrEqualTo(width));
    }
}
