namespace Nx.Playwright;

public static class TestConfig
{
    // Override with NX_DEMO_URL when the demo runs on another port (Treeline, CI).
    public static readonly string[] AppUrls =
    {
        Environment.GetEnvironmentVariable("NX_DEMO_URL")?.TrimEnd('/') ?? "http://localhost:44420",
    };
}
