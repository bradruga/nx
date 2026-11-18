namespace Nx.Demo.Data;

public sealed record Customer(int Id, string Code, string Name, string Region, string City);

public sealed record Employee(int Id, string Name, string Department, string Title);

public sealed record Part(int Id, string Number, string Description, string Category);

public sealed record Place(string City, string County, string State);

/// <summary>Deterministic demo data. Same lists on every run so the Playwright tests can rely on them.</summary>
public static class SampleData
{
    public static readonly IReadOnlyList<string> States =
    [
        "Connecticut", "Delaware", "Maine", "Maryland", "Massachusetts", "New Hampshire",
        "New Jersey", "New York", "Pennsylvania", "Rhode Island", "Vermont", "Virginia",
    ];

    /// <summary>Towns with their counties, for the NxAutoComplete demo.</summary>
    public static readonly IReadOnlyList<Place> Places =
    [
        new("Albany", "Albany County", "NY"),         new("Allentown", "Lehigh County", "PA"),
        new("Bangor", "Penobscot County", "ME"),      new("Barre", "Washington County", "VT"),
        new("Berlin", "Coös County", "NH"),           new("Burlington", "Chittenden County", "VT"),
        new("Cambridge", "Middlesex County", "MA"),   new("Concord", "Merrimack County", "NH"),
        new("Hartford", "Hartford County", "CT"),     new("Hyannis", "Barnstable County", "MA"),
        new("Los Angeles", "Los Angeles County", "CA"), new("Lowell", "Middlesex County", "MA"),
        new("Manchester", "Hillsborough County", "NH"), new("Montpelier", "Washington County", "VT"),
        new("Nashua", "Hillsborough County", "NH"),   new("New Haven", "New Haven County", "CT"),
        new("New York", "New York County", "NY"),     new("Newport", "Newport County", "RI"),
        new("Portland", "Cumberland County", "ME"),   new("Portsmouth", "Rockingham County", "NH"),
        new("Providence", "Providence County", "RI"), new("Rutland", "Rutland County", "VT"),
        new("Salem", "Essex County", "MA"),           new("Scranton", "Lackawanna County", "PA"),
        new("Springfield", "Hampden County", "MA"),   new("Stamford", "Fairfield County", "CT"),
        new("Syracuse", "Onondaga County", "NY"),     new("Utica", "Oneida County", "NY"),
        new("Waterbury", "New Haven County", "CT"),   new("Wilmington", "New Castle County", "DE"),
        new("Worcester", "Worcester County", "MA"),
    ];

    public static readonly IReadOnlyList<string> ShipVia =
    [
        "Customer pickup", "Flatbed", "Lowboy", "Our truck", "Rail", "Step deck",
    ];

    public static readonly IReadOnlyList<Customer> Customers =
    [
        new(1,  "ACME",  "Acme Site Development",       "New England",   "Hartford"),
        new(2,  "BALD",  "Baldwin Paving",              "New England",   "Worcester"),
        new(3,  "BRKS",  "Brookside Utilities",         "Mid-Atlantic",  "Allentown"),
        new(4,  "CAPE",  "Cape Cod Marine Works",       "New England",   "Hyannis"),
        new(5,  "CASC",  "Cascade Civil",               "New York",      "Albany"),
        new(6,  "DELV",  "Delaware Valley Contractors", "Mid-Atlantic",  "Wilmington"),
        new(7,  "EMPR",  "Empire Infrastructure",       "New York",      "Syracuse"),
        new(8,  "GRAN",  "Granite State Builders",      "New England",   "Manchester"),
        new(9,  "HUDS",  "Hudson Highway Group",        "New York",      "Poughkeepsie"),
        new(10, "KEYS",  "Keystone Bridge & Rail",      "Mid-Atlantic",  "Harrisburg"),
        new(11, "KIEW",  "Kiewit Northeast",            "New York",      "Newburgh"),
        new(12, "LAKE",  "Lakeshore Excavation",        "New York",      "Buffalo"),
        new(13, "MERR",  "Merrimack Concrete",          "New England",   "Lowell"),
        new(14, "NARR",  "Narragansett Construction",   "New England",   "Providence"),
        new(15, "PATR",  "Patriot Earthworks",          "New England",   "Boston"),
        new(16, "PINE",  "Pine Barrens Site Co.",       "Mid-Atlantic",  "Toms River"),
        new(17, "SUSQ",  "Susquehanna Structures",      "Mid-Atlantic",  "Lancaster"),
        new(18, "TACO",  "Taconic Drainage",            "New York",      "Kingston"),
        new(19, "VERM",  "Vermont Precast Erectors",    "New England",   "Burlington"),
        new(20, "WHIT",  "White Mountain Utilities",    "New England",   "Conway"),
    ];

    public static readonly IReadOnlyList<Employee> Employees =
    [
        new(1, "Dana Ortiz",      "Engineering", "Project Engineer"),
        new(2, "Marcus Bell",     "Production",  "Plant Manager"),
        new(3, "Priya Raman",     "Engineering", "Structural Engineer"),
        new(4, "Tom Kowalski",    "Shipping",    "Dispatcher"),
        new(5, "Elena Vasquez",   "Sales",       "Estimator"),
        new(6, "Grace Liu",       "Quality",     "QC Inspector"),
        new(7, "Sam Whitaker",    "Production",  "Batch Plant Operator"),
        new(8, "Hannah O'Neill",  "Sales",       "Account Manager"),
        new(9, "Jordan Price",    "Shipping",    "Yard Lead"),
        new(10, "Victor Moreau",  "Quality",     "Lab Technician"),
    ];

    private static readonly string[] PartCategories = ["Box Culvert", "Catch Basin", "Manhole", "Pipe", "Headwall", "Barrier", "Vault", "Slab"];
    private static readonly string[] PartSizes = ["24\"", "36\"", "48\"", "60\"", "72\"", "4'x4'", "6'x4'", "8'x6'", "10'x8'"];
    private static readonly string[] PartGrades = ["Std", "HS-20", "HL-93", "Heavy", "Light"];

    private static IReadOnlyList<Part>? parts;

    /// <summary>50,000 precast parts, for the virtualization demo.</summary>
    public static IReadOnlyList<Part> Parts => parts ??= BuildParts(50_000);

    private static List<Part> BuildParts(int count)
    {
        var random = new Random(42);
        var list = new List<Part>(count);
        for (var i = 1; i <= count; i++)
        {
            var category = PartCategories[random.Next(PartCategories.Length)];
            var size = PartSizes[random.Next(PartSizes.Length)];
            var grade = PartGrades[random.Next(PartGrades.Length)];
            list.Add(new Part(i, $"P-{i:00000}", $"{category} {size} {grade}", category));
        }
        return list;
    }

    /// <summary>A fake remote search over <see cref="Customers"/>, with latency, for the DataProvider demo.</summary>
    public static async Task<IEnumerable<Customer>> SearchCustomersAsync(string filter, int delayMs, CancellationToken token)
    {
        await Task.Delay(delayMs, token);
        return Customers.Where(c =>
            c.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
            c.Code.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
            c.City.Contains(filter, StringComparison.OrdinalIgnoreCase));
    }
}
