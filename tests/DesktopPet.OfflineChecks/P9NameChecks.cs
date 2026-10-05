using System.Text.Json;
using DesktopPet.Models;

public static class P9NameChecks
{
    public static void Run(Action<bool, string> check)
    {
        var failures = new List<string>();
        void Probe(bool condition, string name)
        {
            if (condition) check(true, name);
            else { failures.Add(name); Console.WriteLine("FAIL: " + name); }
        }

        // A UTF-16 substring at offset 20 cuts the last dog's surrogate pair.
        var boundary = new string('A', 19) + "🐶";
        var normalized = new AppSettings { PetName = boundary + "Z" }.Normalize();
        Probe(normalized.PetName == boundary, "P9 the twentieth name character keeps its complete emoji");
        var dogs = string.Concat(Enumerable.Repeat("🐶", 20));
        Probe(new AppSettings { PetName = dogs + "Z" }.Normalize().PetName == dogs,
            "P9 twenty emoji count as twenty name characters");
        var family = "👨‍👩‍👧‍👦";
        var familyName = new string('A', 19) + family;
        Probe(new AppSettings { PetName = familyName + "Z" }.Normalize().PetName == familyName,
            "P9 a joined emoji remains whole at the name limit");
        var accents = string.Concat(Enumerable.Repeat("e\u0301", 20));
        Probe(new AppSettings { PetName = accents + "Z" }.Normalize().PetName == accents,
            "P9 combining accents remain attached to their name characters");
        var flags = new string('A', 19) + "🇨🇳";
        Probe(new AppSettings { PetName = flags + "Z" }.Normalize().PetName == flags,
            "P9 a flag emoji remains whole at the name limit");
        var json = JsonSerializer.Serialize(normalized);
        Probe(JsonSerializer.Deserialize<AppSettings>(json)?.PetName == boundary,
            "P9 the boundary emoji survives JSON save and reload without replacement characters");
        Probe(new AppSettings { PetName = "  团团  " }.Normalize().PetName == "团团" &&
            new AppSettings { PetName = " " }.Normalize().PetName == "团团",
            "P9 existing name trimming and empty-name fallback still work");
        check(failures.Count == 0, "P9 name checks: " + string.Join("; ", failures));
    }
}
