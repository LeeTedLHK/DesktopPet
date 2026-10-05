using System.Reflection;
using System.Text.Json;
using DesktopPet.Animations;
using DesktopPet.Models;
using DesktopPet.Services;
using DesktopPet.Skins;
using DesktopPet.Views;

internal static class P8SkinIntegrationChecks
{
    public static void Run(PetWindow pet, Action<bool, string> check, string evidence)
    {
        var builtIn = Path.Combine(AppContext.BaseDirectory, "Assets", "Pet");
        var root = Path.Combine(evidence, "p8-real-skin-roundtrip"); Directory.CreateDirectory(root);
        var catalog = new SkinCatalog(builtIn, Path.Combine(root, "skins"));
        var zip = Path.Combine(root, "default-export.zip");
        catalog.ExportZip("default", zip);
        var imported = catalog.ImportZip(zip);
        check(imported.MissingActions.Count == 0 &&
            Directory.EnumerateFiles(imported.RootPath, "*.png", SearchOption.AllDirectories).Count() == 172,
            "P8 actual default skin export/import preserves every authored action and frame");
        check(File.ReadAllBytes(Path.Combine(imported.RootPath, "mouth-anchors.json")).SequenceEqual(
            File.ReadAllBytes(Path.Combine(builtIn, "mouth-anchors.json"))),
            "P8 actual default skin roundtrip preserves measured mouth metadata bytes");
        try
        {
            check(pet.TryLoadSkin(imported.RootPath, out var error), "P8 exported actual skin fully decodes in WPF: " + error);
            var animation = (AnimationManager)typeof(PetWindow).GetField("_animationManager",
                BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(pet)!;
            animation.SetState(PetState.CarryingRight, true, false);
            using var anchors = JsonDocument.Parse(File.ReadAllText(Path.Combine(builtIn, "mouth-anchors.json")));
            var frame = anchors.RootElement.GetProperty("actions").GetProperty("CarryRight")[0];
            check(animation.CurrentMouthAnchor == new PetPoint(frame.GetProperty("x").GetDouble(), frame.GetProperty("y").GetDouble()),
                "P8 imported actual carrying pose uses its exported mouth anchor in WPF");
        }
        finally { check(pet.TryLoadSkin(builtIn, out _), "P8 native skin roundtrip restores the original default skin"); }
    }
}
