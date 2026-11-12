namespace Nx.Demo.Data;

/// <summary>The sample PDF the viewer demos open, as bytes (wwwroot/samples/pour-report.pdf) or by its address.</summary>
public static class DemoPdf
{
    public const string Url = "samples/pour-report.pdf";

    public static byte[] Read(IWebHostEnvironment env) => File.ReadAllBytes(Path.Combine(env.WebRootPath, "samples", "pour-report.pdf"));
}
