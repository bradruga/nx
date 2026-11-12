namespace Nx.Demo.Data;

/// <summary>A folder or file as ERP's project files come: flat, with Telerik's field names.</summary>
public sealed class FileEntry
{
    public string Id { get; init; } = "";
    public string? ParentId { get; init; }
    public string Name { get; init; } = "";
    public string? Extension { get; init; }
    public bool IsDirectory { get; init; }
    public long Size { get; init; }
    public DateTime DateCreated { get; init; }
    public DateTime DateModified { get; init; }
}

/// <summary>A precast project's file share.</summary>
public static class DemoFiles
{
    public static IReadOnlyList<FileEntry> Project { get; } = Build();

    private static List<FileEntry> Build()
    {
        var list = new List<FileEntry>();
        var day = new DateTime(2026, 10, 1, 9, 0, 0);
        var n = 0;
        string Folder(string name, string? parent)
        {
            var id = $"d{n++}";
            list.Add(new FileEntry { Id = id, ParentId = parent, Name = name, IsDirectory = true, DateCreated = day, DateModified = day.AddDays(n) });
            return id;
        }
        void File(string name, string ext, long kb, string parent) =>
            list.Add(new FileEntry { Id = $"f{n++}", ParentId = parent, Name = name, Extension = ext, Size = kb * 1024,
                DateCreated = day.AddDays(n % 20), DateModified = day.AddDays(n % 30).AddHours(n % 8) });

        var drawings = Folder("Drawings", null);
        var shop = Folder("Shop", drawings);
        var erection = Folder("Erection", drawings);
        var quality = Folder("Quality", null);
        var cylinders = Folder("Cylinder breaks", quality);
        var photos = Folder("Photos", null);
        Folder("Archive", null);

        File("WP-12 shop drawing", ".pdf", 2_480, shop);
        File("WP-13 shop drawing", ".pdf", 2_310, shop);
        File("WP-14 shop drawing", ".pdf", 2_655, shop);
        File("Embed schedule", ".xlsx", 86, shop);
        File("Erection plan rev 3", ".pdf", 5_120, erection);
        File("Crane picks", ".xlsx", 44, erection);
        File("Pour report P-14", ".pdf", 340, quality);
        File("Inspection checklist", ".docx", 58, quality);
        File("Cylinder log 7-day", ".xlsx", 31, cylinders);
        File("Cylinder log 28-day", ".xlsx", 33, cylinders);
        File("WP-12 stripped", ".jpg", 3_870, photos);
        File("WP-12 reveal detail", ".jpg", 2_940, photos);
        File("Bug holes patched", ".png", 1_180, photos);
        File("Project contacts", ".txt", 2, drawings);
        list.Add(new FileEntry { Id = "f-readme", ParentId = null, Name = "Read me", Extension = ".txt", Size = 1_200, DateCreated = day, DateModified = day });
        list.Add(new FileEntry { Id = "f-contract", ParentId = null, Name = "Contract", Extension = ".pdf", Size = 812_000, DateCreated = day, DateModified = day.AddDays(3) });
        return list;
    }
}
