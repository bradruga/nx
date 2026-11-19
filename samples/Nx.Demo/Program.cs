using Nx.Components;
using Nx.Demo.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddNxDialogs();
builder.Services.AddScoped<Nx.Demo.Data.SiteSearchState>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAntiforgery();

app.MapStaticAssets();

// The NxUpload demo's save URL: reads the file and answers with a made-up temp id, as ERP's file API does. A file
// whose name contains "fail" gets a 500, to show a failed upload; nothing is stored.
app.MapPost("/demo/upload", async (HttpRequest request) =>
{
    var form = await request.ReadFormAsync();
    var file = form.Files.GetFile("files");
    if (file is null)
        return Results.BadRequest("No file");
    await using (var stream = file.OpenReadStream())
        await stream.CopyToAsync(Stream.Null);
    await Task.Delay(400);
    return file.FileName.Contains("fail", StringComparison.OrdinalIgnoreCase)
        ? Results.Problem("The demo refuses files named fail.", statusCode: 500)
        : Results.Text($"tmp-{Guid.NewGuid():N}"[..12]);
}).DisableAntiforgery();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
