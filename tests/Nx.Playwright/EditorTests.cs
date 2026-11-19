using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace Nx.Playwright;

/// <summary>NxEditor: contenteditable, the selection, execCommand, paste and focus only exist in a real browser.</summary>
[TestFixtureSource(typeof(TestConfig), nameof(TestConfig.AppUrls))]
public class EditorTests : PageTest
{
    private readonly string _baseUrl;

    public EditorTests(string baseUrl) => _baseUrl = baseUrl;

    private async Task GoToPage(string page)
    {
        await Page.GotoAsync($"{_baseUrl}/editor/{page}");
        await Expect(Page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();
    }

    private ILocator Tool(string editor, string command) => Page.Locator($".nx-editor:has(#{editor}) [data-nx-editor-cmd={command}]");

    private Task<string> Html(string editor) => Page.Locator($"#{editor}").InnerHTMLAsync();

    [Test]
    public async Task Type_Bold_RaisesTheValue()
    {
        await GoToPage("overview");
        var editor = Page.Locator("#overview-editor");
        await editor.ClickAsync();
        await Page.Keyboard.PressAsync("Control+End");
        await Page.Keyboard.PressAsync("Enter");
        await Page.Keyboard.TypeAsync("Bring the vibrator");
        await Page.Keyboard.PressAsync("Shift+Home");
        await Tool("overview-editor", "bold").ClickAsync();

        await Expect(Tool("overview-editor", "bold")).ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(Page.Locator("#overview-html")).ToContainTextAsync("<b>Bring the vibrator</b>");
        // The toolbar button did not take the focus from the text.
        Assert.That(await editor.EvaluateAsync<bool>("e => document.activeElement === e"), Is.True);
    }

    [Test]
    public async Task Undo_Redo_OwnHistory()
    {
        await GoToPage("examples");
        var editor = Page.Locator("#comment-editor");
        await editor.ClickAsync();
        await Page.Keyboard.TypeAsync("First");
        await Page.Keyboard.PressAsync("Control+a");
        // Tools run after a server round trip: wait for the HTML rather than reading it once.
        await Tool("comment-editor", "italic").ClickAsync();
        await WaitForHtml("comment-editor", "<p><i>First</i></p>");

        await Page.Keyboard.PressAsync("Control+z");
        await WaitForHtml("comment-editor", "<p>First</p>");
        await Expect(Tool("comment-editor", "redo")).ToBeEnabledAsync();
        await Page.Keyboard.PressAsync("Control+y");
        await WaitForHtml("comment-editor", "<p><i>First</i></p>");
    }

    private Task WaitForHtml(string editor, string html) =>
        Page.WaitForFunctionAsync("([id, html]) => document.getElementById(id).innerHTML === html", new[] { editor, html });

    [Test]
    public async Task Paste_Word_CleansAndMakesLists()
    {
        await GoToPage("examples");
        var editor = Page.Locator("#comment-editor");
        await editor.ClickAsync();
        await editor.EvaluateAsync("""
            el => {
                const dt = new DataTransfer();
                dt.setData('text/html', `<!--StartFragment--><p class=MsoListParagraph style='mso-list:l0 level1 lfo1'><span style='mso-list:Ignore'>1.<span>&nbsp;</span></span>Forms</p>`
                    + `<p class=MsoListParagraph style='mso-list:l0 level1 lfo1'><span style='mso-list:Ignore'>2.</span>Rebar</p>`
                    + `<script>window.pwned = 1</script><a href="javascript:alert(1)" onclick="x()">bad</a><img src="file:///c:/x.png">`);
                el.dispatchEvent(new ClipboardEvent('paste', { clipboardData: dt, bubbles: true, cancelable: true }));
            }
            """);

        var html = await Html("comment-editor");
        Assert.That(html, Does.Contain("<ol><li>Forms</li><li>Rebar</li></ol>"));
        Assert.That(html, Does.Not.Contain("script").And.Not.Contain("javascript").And.Not.Contain("onclick")
            .And.Not.Contain("Mso").And.Not.Contain("<img"));
    }

    [Test]
    public async Task Paste_Web_DropsScriptsAndClasses()
    {
        await GoToPage("examples");
        var editor = Page.Locator("#comment-editor");
        await editor.ClickAsync();
        await editor.EvaluateAsync("""
            el => {
                const dt = new DataTransfer();
                dt.setData('text/html', `<html><body><!--StartFragment--><div class="article-body"><p class="lead" style="color: rgb(200, 0, 0)">Slump <b class="hl">4 in</b></p>`
                    + `<script>window.pwned = 1</script></div><!--EndFragment--></body></html>`);
                dt.setData('text/plain', 'Slump 4 in');
                el.dispatchEvent(new ClipboardEvent('paste', { clipboardData: dt, bubbles: true, cancelable: true }));
            }
            """);

        await Expect(editor).ToContainTextAsync("Slump 4 in");
        var html = await Html("comment-editor");
        Assert.That(html, Does.Not.Contain("<script").And.Not.Contain("pwned").And.Not.Contain("class="));
        Assert.That(html, Does.Contain("<b>4 in</b>"), "the formatting stays");
        Assert.That(await Page.EvaluateAsync<bool>("() => window.pwned === undefined"), Is.True);
    }

    /// <summary>Selects "Notes" in the report's last paragraph, wherever the styles so far wrapped it.</summary>
    private async Task SelectNotesAsync()
    {
        var editor = Page.Locator("#report-editor");
        await editor.Locator("p", new() { HasText = "Notes:" }).ClickAsync();
        await editor.EvaluateAsync("""
            el => {
                const p = [...el.querySelectorAll('p')].find(p => p.textContent === 'Notes:');
                const text = document.createTreeWalker(p, NodeFilter.SHOW_TEXT).nextNode();
                const range = document.createRange();
                range.setStart(text, 0);
                range.setEnd(text, 5);
                getSelection().removeAllRanges();
                getSelection().addRange(range);
            }
            """);
    }

    [Test]
    public async Task FontSizeAndColors_StyledSpans_NoFontTags()
    {
        await GoToPage("examples");
        var editor = Page.Locator("#report-editor");
        var tools = Page.Locator(".nx-editor:has(#report-editor)");
        var popup = Page.Locator(".nx-combobox-popup");

        // Selected again before each tool: a style tool does not keep the selection (see StyleTool_KeepsTheSelection).
        await SelectNotesAsync();
        await tools.Locator(".nx-editor-fontfamily .nx-combobox-field").ClickAsync();
        await popup.GetByRole(AriaRole.Option, new() { Name = "Georgia", Exact = true }).ClickAsync();
        await Page.WaitForFunctionAsync("() => /font-family:[^;\"]*Georgia/.test(document.getElementById('report-editor').innerHTML)");

        await SelectNotesAsync();
        await tools.Locator(".nx-editor-fontsize .nx-combobox-field").ClickAsync();
        await popup.GetByRole(AriaRole.Option, new() { Name = "18 pt", Exact = true }).ClickAsync();
        await Page.WaitForFunctionAsync("() => /font-size:\\s*18pt/.test(document.getElementById('report-editor').innerHTML)");

        await SelectNotesAsync();
        await tools.Locator(".nx-editor-color button[aria-expanded]").Nth(0).ClickAsync();
        await Page.Locator(".nx-colorpicker-popup .nx-colorpicker-cell[aria-label='#ed7e32']").ClickAsync();
        await Page.WaitForFunctionAsync("() => /[^-]color:\\s*rgb\\(237, 126, 50\\)/.test(document.getElementById('report-editor').innerHTML)");

        await SelectNotesAsync();
        await tools.Locator(".nx-editor-color button[aria-expanded]").Nth(1).ClickAsync();
        await Page.Locator(".nx-colorpicker-popup .nx-colorpicker-cell[aria-label='#fce5d4']").ClickAsync();
        await Page.WaitForFunctionAsync("() => /background-color:\\s*rgb\\(252, 229, 212\\)/.test(document.getElementById('report-editor').innerHTML)");

        var html = await Html("report-editor");
        Assert.That(html, Does.Not.Contain("<font"));
        // All four on the selected word, through spans; the rest of the line untouched.
        var styled = await editor.EvaluateAsync<string[]>("""
            el => {
                const p = [...el.querySelectorAll('p')].find(p => p.textContent === 'Notes:');
                const walker = document.createTreeWalker(p, NodeFilter.SHOW_TEXT);
                const out = [];
                for (let n = walker.nextNode(); n; n = walker.nextNode()) {
                    const s = getComputedStyle(n.parentElement);
                    out.push(`${n.textContent}|${s.fontFamily.split(',')[0].replace(/"/g, '')}|${s.fontSize}|${s.color}|${s.backgroundColor}`);
                }
                return out;
            }
            """);
        Assert.That(styled, Has.Some.EqualTo("Notes|Georgia|24px|rgb(237, 126, 50)|rgb(252, 229, 212)"));
        Assert.That(styled.Single(s => s.StartsWith(":|")), Does.Not.Contain("Georgia"));
    }

    [Test]
    public async Task StyleTool_KeepsTheSelection()
    {
        await GoToPage("examples");
        var tools = Page.Locator(".nx-editor:has(#report-editor)");
        await SelectNotesAsync();
        await tools.Locator(".nx-editor-fontsize .nx-combobox-field").ClickAsync();
        await Page.Locator(".nx-combobox-popup").GetByRole(AriaRole.Option, new() { Name = "18 pt", Exact = true }).ClickAsync();
        await Page.WaitForFunctionAsync("() => /font-size:\\s*18pt/.test(document.getElementById('report-editor').innerHTML)");
        await Page.WaitForFunctionAsync("() => getSelection().toString() === 'Notes'");

        await tools.Locator(".nx-editor-color button[aria-expanded]").Nth(0).ClickAsync();
        await Page.Locator(".nx-colorpicker-popup .nx-colorpicker-cell[aria-label='#ed7e32']").ClickAsync();
        await Page.WaitForFunctionAsync("() => /[^-]color:\\s*rgb\\(237, 126, 50\\)/.test(document.getElementById('report-editor').innerHTML)");
    }

    [Test]
    public async Task Link_CtrlK_EnterInserts()
    {
        await GoToPage("examples");
        var editor = Page.Locator("#email-editor");
        await editor.Locator("li", new() { HasText = "Trucks" }).ClickAsync();
        await Page.Keyboard.PressAsync("End");
        await Page.Keyboard.PressAsync("Control+k");
        await Page.Locator("#email-editor-link-href").FillAsync("https://example.com/trucks");
        await Page.Locator("#email-editor-link-text").FillAsync("dispatch");
        await Page.Keyboard.PressAsync("Enter");

        await Expect(Page.Locator("#email-editor-link-href")).ToHaveCountAsync(0);
        await Expect(editor.Locator("a[href='https://example.com/trucks']")).ToHaveTextAsync("dispatch");
        await Expect(Tool("email-editor", "unlink")).ToBeEnabledAsync();
    }

    [Test]
    public async Task Table_Picker_InsertsAndTabMovesCells()
    {
        await GoToPage("examples");
        var editor = Page.Locator("#report-editor");
        await editor.Locator("p", new() { HasText = "Notes:" }).ClickAsync();
        await Page.Keyboard.PressAsync("End");
        await Tool("report-editor", "insertTable").ClickAsync();

        var picker = Page.Locator(".nx-editor-tablepicker");
        await Expect(picker).ToBeVisibleAsync();
        // The picker takes keys once nx-editor.js has opened it, a round trip after it shows; its label says so.
        await Expect(picker.Locator(".nx-editor-tablepicker-label")).ToHaveTextAsync("1 × 1 table");
        await Page.Keyboard.PressAsync("ArrowRight");
        await Page.Keyboard.PressAsync("ArrowDown");
        await Expect(picker.Locator(".nx-editor-tablepicker-label")).ToHaveTextAsync("2 × 2 table");
        await Page.Keyboard.PressAsync("Enter");

        await Expect(picker).ToHaveCountAsync(0);
        await Expect(editor.Locator("table")).ToHaveCountAsync(2);
        await Page.Keyboard.TypeAsync("A");
        await Page.Keyboard.PressAsync("Tab");
        await Page.Keyboard.TypeAsync("B");
        var cells = await editor.Locator("table").Nth(1).Locator("td").AllTextContentsAsync();
        Assert.That(cells.Take(2), Is.EqualTo(new[] { "A", "B" }));
    }

    [Test]
    public async Task Table_AddRow_Merge_Split()
    {
        await GoToPage("examples");
        var editor = Page.Locator("#report-editor");
        await editor.Locator("td", new() { HasText = "Dimensions" }).ClickAsync();
        await Tool("report-editor", "addRowAfter").ClickAsync();
        await Expect(editor.Locator("tr")).ToHaveCountAsync(4);

        await editor.EvaluateAsync("""
            el => {
                const cells = [...el.querySelector('table').rows[1].cells];
                const range = document.createRange();
                range.setStart(cells[1].firstChild, 0);
                range.setEnd(cells[2].firstChild, 1);
                getSelection().removeAllRanges();
                getSelection().addRange(range);
            }
            """);
        await Tool("report-editor", "mergeCells").ClickAsync();
        await Expect(editor.Locator("td[colspan='2']")).ToHaveCountAsync(1);

        await Tool("report-editor", "splitCell").ClickAsync();
        await Expect(editor.Locator("td[colspan]")).ToHaveCountAsync(0);
        await Expect(editor.Locator("tr").Nth(1).Locator("td")).ToHaveCountAsync(3);
    }

    [Test]
    public async Task Validation_EmptyIsRequired()
    {
        await GoToPage("examples");
        await Page.Locator("#editor-submit").ClickAsync();
        await Expect(Page.Locator("#hold-editor")).ToHaveAttributeAsync("aria-invalid", "true");
        await Expect(Page.Locator(".validation-message")).ToContainTextAsync("Give a reason.");

        await Page.Locator("#hold-editor").ClickAsync();
        await Page.Keyboard.TypeAsync("Rain");
        await Page.Locator("#editor-submit").ClickAsync();
        await Expect(Page.Locator("#editor-result")).ToHaveTextAsync("Hold placed");
    }

    [Test]
    public async Task Playground_SetFromCode_ReplacesTheText()
    {
        await GoToPage("playground");
        await Page.Locator("#playground-set").ClickAsync();
        await Expect(Page.Locator("#playground-editor")).ToContainTextAsync("Set from code at");
        await Expect(Page.Locator("#playground-changes")).ToHaveTextAsync("ValueChanged × 0");

        await Page.Locator("#pg-readonly").CheckAsync();
        await Expect(Page.Locator("#playground-editor")).ToHaveAttributeAsync("contenteditable", "false");
        await Expect(Tool("playground-editor", "bold")).ToBeDisabledAsync();
    }

    private ILocator ImageBox => Page.Locator(".nx-editor:has(#picture-editor) .nx-editor-image-box");

    private async Task DragAsync(ILocator from, float dx, float dy)
    {
        var box = (await from.BoundingBoxAsync())!;
        await Page.Mouse.MoveAsync(box.X + box.Width / 2, box.Y + box.Height / 2);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(box.X + box.Width / 2 + dx, box.Y + box.Height / 2 + dy, new() { Steps = 6 });
        await Page.Mouse.UpAsync();
    }

    [Test]
    public async Task Picture_Click_ShowsHandlesOverIt_EscapeLetsGo()
    {
        await GoToPage("examples");
        var image = Page.Locator("#picture-editor img");
        await image.ClickAsync();

        await Expect(ImageBox.Locator(".nx-editor-image-handle")).ToHaveCountAsync(4);
        var picture = (await image.BoundingBoxAsync())!;
        var outline = (await ImageBox.BoundingBoxAsync())!;
        Assert.That(outline.X, Is.EqualTo(picture.X).Within(1));
        Assert.That(outline.Y, Is.EqualTo(picture.Y).Within(1));
        Assert.That(outline.Width, Is.EqualTo(picture.Width).Within(1));
        // The handles sit in the editor, not in the text: never in the value.
        Assert.That(await Html("picture-editor"), Does.Not.Contain("nx-editor-image"));

        await Page.Keyboard.PressAsync("Escape");
        await Expect(ImageBox).ToHaveCountAsync(0);
        await Expect(image).ToHaveCountAsync(1);
    }

    [Test]
    public async Task Picture_CornerDrag_ResizesInProportion_RaisesTheValue_OneUndoStep()
    {
        await GoToPage("examples");
        var image = Page.Locator("#picture-editor img");
        await image.ClickAsync();
        await DragAsync(ImageBox.Locator(".nx-editor-image-handle-se"), 80, 20);

        await Expect(image).ToHaveAttributeAsync("width", "320");
        await Expect(image).ToHaveAttributeAsync("height", "180");
        await Expect(Page.Locator("#picture-html")).ToContainTextAsync("width=\"320\" height=\"180\"");
        await Expect(Page.Locator("#picture-html")).Not.ToContainTextAsync("nx-editor-image");
        // Still selected, the outline on the resized picture; the text kept the focus.
        Assert.That((await ImageBox.BoundingBoxAsync())!.Width, Is.EqualTo(320).Within(1));
        Assert.That(await Page.Locator("#picture-editor").EvaluateAsync<bool>("e => document.activeElement === e"), Is.True);

        await Page.Keyboard.PressAsync("Control+z");
        await Expect(image).ToHaveAttributeAsync("width", "240");
        await Expect(ImageBox).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Picture_Resize_StopsAtTheTextWidth()
    {
        await GoToPage("examples");
        var image = Page.Locator("#picture-editor img");
        await image.ClickAsync();
        await DragAsync(ImageBox.Locator(".nx-editor-image-handle-se"), 3000, 0);

        var width = int.Parse((await image.GetAttributeAsync("width"))!);
        var room = await Page.Locator("#picture-editor").EvaluateAsync<int>(
            "e => Math.round(e.clientWidth - parseFloat(getComputedStyle(e).paddingLeft) - parseFloat(getComputedStyle(e).paddingRight))");
        Assert.That(width, Is.EqualTo(room).Within(1));
    }

    [Test]
    public async Task Picture_Delete_RemovesIt_ClickingTextLetsGo()
    {
        await GoToPage("examples");
        var image = Page.Locator("#picture-editor img");
        await image.ClickAsync();
        await Page.Locator("#picture-editor p").First.ClickAsync();
        await Expect(ImageBox).ToHaveCountAsync(0);

        await image.ClickAsync();
        await Page.Keyboard.PressAsync("Delete");
        await Expect(image).ToHaveCountAsync(0);
        await Expect(ImageBox).ToHaveCountAsync(0);
        await Expect(Page.Locator("#picture-html")).Not.ToContainTextAsync("<img");
    }

    [Test]
    public async Task Picture_Drag_MovesItInTheText()
    {
        await GoToPage("examples");
        var image = Page.Locator("#picture-editor img");
        await Page.Locator("#picture-editor").ScrollIntoViewIfNeededAsync();   // mouse events below the viewport go nowhere
        var target = (await Page.Locator("#picture-editor p").Last.BoundingBoxAsync())!;
        var from = (await image.BoundingBoxAsync())!;
        await Page.Mouse.MoveAsync(from.X + 40, from.Y + 40);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(from.X + 50, from.Y + 50, new() { Steps = 3 });
        await Page.Mouse.MoveAsync(target.X + 60, target.Y + target.Height / 2, new() { Steps = 8 });
        await Page.Mouse.UpAsync();

        // The same picture, once, now inside the last paragraph.
        await Expect(Page.Locator("#picture-editor p").Last.Locator("img")).ToHaveCountAsync(1);
        await Expect(image).ToHaveCountAsync(1);
        await Expect(image).ToHaveAttributeAsync("width", "240");
    }

    [Test]
    public async Task Picture_ReadOnly_IsNotSelected()
    {
        await GoToPage("playground");
        await Page.Locator("#playground-editor").EvaluateAsync(
            "e => e.insertAdjacentHTML('beforeend', '<p><img src=\"samples/wall-panel.svg\" width=\"120\" height=\"68\"></p>')");
        await Page.Locator("#pg-readonly").CheckAsync();
        await Expect(Page.Locator("#playground-editor")).ToHaveAttributeAsync("contenteditable", "false");

        await Page.Locator("#playground-editor img").ClickAsync();
        await Expect(Page.Locator(".nx-editor-image-box")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Placeholder_ShowsOnlyWhileEmpty()
    {
        await GoToPage("examples");
        var editor = Page.Locator("#comment-editor");
        await Expect(editor).ToHaveAttributeAsync("data-empty", "");
        await editor.ClickAsync();
        await Page.Keyboard.TypeAsync("x");
        await Expect(editor).Not.ToHaveAttributeAsync("data-empty", new Regex(".*"));
        await Expect(Page.Locator("#comment-length")).Not.ToHaveTextAsync("Empty");
    }
}
