using System.Windows.Media;

namespace ApplicationView.Services;

public static class FontCatalog
{
    private static readonly string[] Preferred =
    [
        "Courier New", "Consolas", "Lucida Console", "Cascadia Mono",
        "Segoe UI", "Arial", "Tahoma", "Times New Roman",
        "Microsoft Sans Serif", "Verdana", "Calibri", "Georgia"
    ];

    public static IReadOnlyList<string> Families { get; } = Build();

    private static IReadOnlyList<string> Build()
    {
        var system = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var f in Fonts.SystemFontFamilies)
                system.Add(f.Source);
        }
        catch { }

        var list = new List<string>();
        foreach (var name in Preferred)
        {
            if (system.Count == 0 || system.Contains(name))
                list.Add(name);
        }
        foreach (var name in system.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            if (!list.Contains(name, StringComparer.OrdinalIgnoreCase))
                list.Add(name);
        }
        if (list.Count == 0) list.Add("Courier New");
        return list;
    }
}
