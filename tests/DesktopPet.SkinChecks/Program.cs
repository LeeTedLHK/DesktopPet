using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using DesktopPet.Animations;
using DesktopPet.Skins;

var checks = new (string Name, Action<TestWorkspace> Run)[]
{
    ("default tolerates built-in JPG and reference images", DefaultAssets),
    ("unknown and unsafe IDs resolve to default", UnknownId),
    ("folder copies managed frames with GUID ID and natural preview order", FolderImport),
    ("missing actions describe existing animation fallback", MissingActions),
    ("directional running folders import and preserve legacy walking fallback", RunningActions),
    ("manifest name and explicit name persist in a new catalog", ManifestNames),
    ("folder preserves opaque mouth anchors without replacing the manifest name", FolderAnchors),
    ("ZIP imports root actions", ZipRoot),
    ("ZIP imports one enclosing folder", ZipWrapped),
    ("ZIP preserves mouth anchors at root and inside reserved-name wrappers", ZipAnchors),
    ("mouth anchors accept 128 KiB without PNG validation", AnchorsBoundary),
    ("mouth anchors over 128 KiB are rejected before installation", AnchorsSize),
    ("two metadata files do not consume the 5000 frame or ZIP entry budget", AnchorsFrameBudget),
    ("built-in export omits reference images and reimports named frames and anchors", BuiltInExport),
    ("imported export preserves independent frames display name and mouth anchors", ImportedExport),
    ("invalid export source leaves an existing destination intact", ExportValidationFailure),
    ("failed export replacement preserves the target and removes its temporary file", ExportReplacementFailure),
    ("export cannot overwrite or add a file inside its source skin", ExportSourceCollision),
    ("export rejects source collisions through extended Windows paths", ExportExtendedSourceCollision),
    ("export rejects non-ZIP destinations and preserves their existing contents", ExportWrongSuffix),
    ("export rejects linked source and destination ancestors", ExportLinks),
    ("failed imports never expose partial catalog entries", AtomicFailure),
    ("missing or empty Idle is rejected", RequiredIdle),
    ("malformed PNG signature is rejected", MalformedPng),
    ("PNG dimension limits are enforced", PngDimensions),
    ("invalid IHDR fields or checksum are rejected", PngHeader),
    ("truncated PNG structure is rejected", TruncatedPng),
    ("malformed optional metadata is rejected", BadManifest),
    ("unknown files and action folders are rejected", UnknownPayload),
    ("absolute traversal and Windows-special ZIP names are rejected", ZipPaths),
    ("ZIP symlink entries are rejected", ZipLinks),
    ("duplicate ZIP destinations are rejected", ZipDuplicates),
    ("folder junctions are rejected", FolderLinks),
    ("managed root junctions are rejected", ManagedRootLinks),
    ("per-frame decompressed bytes are limited", FrameSize),
    ("total decompressed bytes are limited", TotalSize),
    ("generated metadata stays inside the total byte budget", InstalledTotalSize),
    ("frame count is limited", FrameCount),
    ("manifest bytes are limited", ManifestSize),
    ("aggregate decoded pixels are limited", DecodedPixels),
    ("bad installed manifest is skipped and logged", CatalogCorruption),
    ("catalog is default first then sorted by display name", CatalogOrder),
};
var selected = args.Length == 0 ? checks : checks.Where(check => args.Any(filter => check.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))).ToArray();
var failed = 0;
foreach (var (name, run) in selected)
{
    using var workspace = new TestWorkspace();
    try { run(workspace); Console.WriteLine($"PASS {name}"); }
    catch (Exception error) { failed++; Console.WriteLine($"FAIL {name}: {error}"); }
}
var cleanupComplete = TestWorkspace.CleanupPending();
Console.WriteLine($"Skin checks: {selected.Length - failed}/{selected.Length} passed.");
return failed == 0 && cleanupComplete ? 0 : 1;

static void DefaultAssets(TestWorkspace w)
{
    File.WriteAllText(Path.Combine(w.Default, "pet.jpg"), "built-in fallback does not use import restrictions");
    File.WriteAllText(Path.Combine(w.Default, "reference.png"), "reference");
    var info = w.Catalog().List().Single();
    Equal("default", info.Id);
    Equal(Path.Combine(w.Default, "pet.jpg"), info.PreviewPath);
}
static void UnknownId(TestWorkspace w)
{
    foreach (var id in new[] { "gone", Guid.NewGuid().ToString("N"), "../outside", "C:\\outside", "" })
        Equal("default", w.Catalog().Resolve(id).Id);
}
static void FolderImport(TestWorkspace w)
{
    w.Frame("Idle/frame10.png"); w.Frame("Idle/frame2.png");
    var info = w.Catalog().ImportFolder(w.Source);
    Check(Guid.TryParseExact(info.Id, "N", out _), "imported ID must be GUID-N");
    Equal(Path.Combine(w.Managed, info.Id), info.RootPath);
    Equal("frame2.png", Path.GetFileName(info.PreviewPath));
    File.WriteAllText(Path.Combine(w.Source, "Idle/frame2.png"), "changed external source");
    Equal((byte)137, File.ReadAllBytes(info.PreviewPath!)[0]);
    w.DeleteSource();
    Equal(info.Id, w.Catalog().Resolve(info.Id).Id);
    Equal(2, w.Catalog().List().Count);
}
static void MissingActions(TestWorkspace w)
{
    w.Frame("Idle/idle.png"); w.Frame("Walk/walk.png");
    var info = w.Catalog().ImportFolder(w.Source);
    Check(info.MissingActions.Contains(AnimationKind.Happy), "missing Happy must be reported");
    Check(info.MissingActions.Contains(AnimationKind.WalkLeft), "generic walk does not hide missing directional action");
    Check(!info.MissingActions.Contains(AnimationKind.Idle), "valid Idle must not be missing");
    Check(!info.MissingActions.Contains(AnimationKind.Walk), "valid Walk must not be missing");
    Check(!string.IsNullOrWhiteSpace(info.MissingActionsSummary), "warnings must have display text");
    var clips = new AnimationLoader(info.RootPath).Load();
    Check(clips[AnimationKind.Happy].IsFallback, "Happy must fall back");
    Equal("idle.png", Path.GetFileName(clips[AnimationKind.Happy].Frames[0].Path));
    Equal("walk.png", Path.GetFileName(clips[AnimationKind.WalkLeft].Frames[0].Path));
}
static void RunningActions(TestWorkspace w)
{
    w.Frame("Idle/idle.png"); w.Frame("WalkLeft/left.png"); w.Frame("WalkRight/right.png");
    var legacy = w.Catalog().ImportFolder(w.Source);
    var clips = new AnimationLoader(legacy.RootPath).Load();
    Check(legacy.MissingActions.Contains(AnimationKind.RunLeft) && legacy.MissingActions.Contains(AnimationKind.RunRight),
        "legacy skins report their missing running actions");
    Equal("left.png", Path.GetFileName(clips[AnimationKind.RunLeft].Frames[0].Path));
    Equal("right.png", Path.GetFileName(clips[AnimationKind.RunRight].Frames[0].Path));
    w.Frame("RunLeft/run_left.png"); w.Frame("RunRight/run_right.png");
    var updated = w.Catalog().ImportFolder(w.Source);
    Check(!updated.MissingActions.Contains(AnimationKind.RunLeft) && !updated.MissingActions.Contains(AnimationKind.RunRight),
        "new running folders are accepted in imported folders");
    clips = new AnimationLoader(updated.RootPath).Load();
    Check(!clips[AnimationKind.RunLeft].IsFallback && !clips[AnimationKind.RunRight].IsFallback,
        "imported running frames take precedence over walking");
    var zip = w.Zip(("Idle/idle.png", TestWorkspace.Png()), ("RunLeft/run.png", TestWorkspace.Png()),
        ("RunRight/run.png", TestWorkspace.Png()));
    var zipped = w.Catalog().ImportZip(zip);
    Check(!zipped.MissingActions.Contains(AnimationKind.RunLeft) && !zipped.MissingActions.Contains(AnimationKind.RunRight),
        "ZIP skins preserve both authored running actions");
}
static void ManifestNames(TestWorkspace w)
{
    w.Frame("Idle/idle.png");
    File.WriteAllText(Path.Combine(w.Source, "skin.json"), "{\"name\":\"Cloud 小狗\"}");
    var info = w.Catalog().ImportFolder(w.Source);
    Equal("Cloud 小狗", info.Name);
    Equal(info.Name, w.Catalog().Resolve(info.Id).Name);
    var renamed = w.Catalog().ImportFolder(w.Source, "  My pet  ");
    Equal("My pet", w.Catalog().Resolve(renamed.Id).Name);
}
static void FolderAnchors(TestWorkspace w)
{
    w.Frame("Idle/idle.png");
    w.Write("skin.json", Encoding.UTF8.GetBytes("{\"name\":\"Anchored pet\"}"));
    var anchors = Encoding.UTF8.GetBytes("{\"name\":\"Anchor metadata is not the display name\",\"actions\":{\"CarryLeft\":\"bad action\"}}");
    w.Write("MOUTH-ANCHORS.JSON", anchors);
    var info = w.Catalog().ImportFolder(w.Source);
    Equal("Anchored pet", info.Name);
    Check(File.ReadAllBytes(Path.Combine(info.RootPath, "mouth-anchors.json")).SequenceEqual(anchors), "mouth metadata must remain byte-for-byte unchanged");
    w.DeleteSource();
    Equal(info.Id, w.Catalog().Resolve(info.Id).Id);
    Equal("Anchored pet", w.Catalog().Resolve(info.Id).Name);
}
static void ZipAnchors(TestWorkspace w)
{
    var anchors = Encoding.UTF8.GetBytes("{\"actions\":{\"CarryLeft\":[{\"frame\":0,\"x\":0.2,\"y\":0.5}]}}");
    foreach (var wrapper in new string?[] { null, "MyPet", "Idle", "skin.json", "mouth-anchors.json" })
    {
        var prefix = wrapper is null ? "" : wrapper + "/";
        var entries = new List<(string Name, byte[] Bytes)>
        {
            (prefix + "Mouth-Anchors.json", anchors),
            (prefix + "Idle/", []),
            (prefix + "Idle/idle.png", TestWorkspace.Png()),
            (prefix + "skin.json", Encoding.UTF8.GetBytes("{\"name\":\"ZIP anchored pet\"}")),
        };
        if (wrapper is not null) entries.Insert(0, (prefix, []));
        var info = w.Catalog().ImportZip(w.Zip(entries.ToArray()));
        Equal("ZIP anchored pet", info.Name);
        Check(File.ReadAllBytes(Path.Combine(info.RootPath, "mouth-anchors.json")).SequenceEqual(anchors), "ZIP mouth metadata was changed");
        Equal(info.Id, w.Catalog().Resolve(info.Id).Id);
    }
}
static void AnchorsBoundary(TestWorkspace w)
{
    w.Frame("Idle/idle.png");
    // The animation manager owns per-action validation and fallback; the catalog only bounds this opaque payload.
    var anchors = Enumerable.Repeat((byte)'x', 128 * 1024).ToArray();
    w.Write("mouth-anchors.json", anchors);
    var folder = w.Catalog().ImportFolder(w.Source, "Opaque anchors");
    var zipped = w.Catalog().ImportZip(w.Zip(("Idle/idle.png", TestWorkspace.Png()), ("mouth-anchors.json", anchors)), "Opaque ZIP anchors");
    foreach (var info in new[] { folder, zipped })
    {
        Check(File.ReadAllBytes(Path.Combine(info.RootPath, "mouth-anchors.json")).SequenceEqual(anchors), "128 KiB opaque anchors must survive import");
        Equal(info.Id, w.Catalog().Resolve(info.Id).Id);
    }
}
static void AnchorsSize(TestWorkspace w)
{
    w.Frame("Idle/idle.png");
    var anchors = new byte[128 * 1024 + 1];
    w.Write("mouth-anchors.json", anchors);
    Reject(() => w.Catalog().ImportFolder(w.Source), "anchor");
    Reject(() => w.Catalog().ImportZip(w.Zip(("Idle/idle.png", TestWorkspace.Png()), ("mouth-anchors.json", anchors))), "anchor");
    Equal(0, Directory.EnumerateFileSystemEntries(w.Managed).Count());
}
static void AnchorsFrameBudget(TestWorkspace w)
{
    var png = TestWorkspace.Png();
    var anchors = Encoding.UTF8.GetBytes("{}");
    var manifest = Encoding.UTF8.GetBytes("{\"name\":\"At frame limit\"}");
    for (var i = 0; i < 5000; i++) w.Frame($"Idle/frame{i}.png", png);
    w.Write("skin.json", manifest); w.Write("mouth-anchors.json", anchors);
    Equal("At frame limit", w.Catalog().ImportFolder(w.Source).Name);
    var entries = new List<(string Name, byte[] Bytes)> { ("Pet/", []), ("Pet/skin.json", manifest), ("Pet/mouth-anchors.json", anchors) };
    entries.AddRange(Enum.GetValues<AnimationKind>().Select(kind => ($"Pet/{kind}/", Array.Empty<byte>())));
    entries.AddRange(Enumerable.Range(0, 5000).Select(i => ($"Pet/Idle/frame{i}.png", png)));
    var info = w.Catalog().ImportZip(w.Zip(entries.ToArray()));
    Equal(5000, Directory.EnumerateFiles(Path.Combine(info.RootPath, "Idle")).Count());
}
static void BuiltInExport(TestWorkspace w)
{
    var png = TestWorkspace.Png();
    Directory.CreateDirectory(Path.Combine(w.Default, "Idle"));
    Directory.CreateDirectory(Path.Combine(w.Default, "RunLeft"));
    Directory.CreateDirectory(Path.Combine(w.Default, "References"));
    File.WriteAllBytes(Path.Combine(w.Default, "Idle/frame.png"), png);
    File.WriteAllBytes(Path.Combine(w.Default, "RunLeft/run.png"), png);
    File.WriteAllText(Path.Combine(w.Default, "pet.jpg"), "reference photo");
    File.WriteAllText(Path.Combine(w.Default, "reference.png"), "reference image");
    File.WriteAllText(Path.Combine(w.Default, "References/photo.png"), "reference image");
    File.WriteAllText(Path.Combine(w.Default, "Idle/reference.jpg"), "reference photo");
    var anchors = Encoding.UTF8.GetBytes("{\"actions\":{\"CarryRight\":[]}}");
    File.WriteAllBytes(Path.Combine(w.Default, "mouth-anchors.json"), anchors);
    var destination = Path.Combine(w.Root, "built-in.zip");
    File.WriteAllText(destination, "old target");
    Export(w.Catalog(), "default", destination);
    using (var archive = ZipFile.OpenRead(destination))
    {
        Equal("Idle/frame.png|RunLeft/run.png|mouth-anchors.json|skin.json", string.Join('|', archive.Entries.Select(entry => entry.FullName).Order(StringComparer.Ordinal)));
    }
    var imported = w.Catalog().ImportZip(destination);
    Equal("默认比熊", imported.Name);
    Check(File.ReadAllBytes(Path.Combine(imported.RootPath, "mouth-anchors.json")).SequenceEqual(anchors), "built-in mouth anchors did not roundtrip");
    Check(File.ReadAllBytes(Path.Combine(imported.RootPath, "RunLeft/run.png")).SequenceEqual(png), "built-in running frame did not roundtrip");
}
static void ImportedExport(TestWorkspace w)
{
    w.Frame("Idle/frame10.png"); w.Frame("Idle/frame2.png"); w.Frame("CarryLeft/carry.png");
    var anchors = Encoding.UTF8.GetBytes("{\"actions\":{\"CarryLeft\":[{\"frame\":0,\"x\":0.25,\"y\":0.5}]}}");
    w.Write("mouth-anchors.json", anchors);
    var original = w.Catalog().ImportFolder(w.Source, "Exported 云朵");
    w.DeleteSource();
    var destination = Path.Combine(w.Root, "imported.zip");
    Export(w.Catalog(), original.Id, destination);
    var imported = w.Catalog().ImportZip(destination);
    Equal("Exported 云朵", imported.Name);
    Check(imported.Id != original.Id, "reimport must create an independent installed skin");
    Equal("frame2.png", Path.GetFileName(imported.PreviewPath));
    Check(File.ReadAllBytes(Path.Combine(imported.RootPath, "mouth-anchors.json")).SequenceEqual(anchors), "imported mouth anchors did not roundtrip");
    Check(File.ReadAllBytes(Path.Combine(imported.RootPath, "CarryLeft/carry.png")).SequenceEqual(TestWorkspace.Png()), "imported carrying frame did not roundtrip");
}
static void ExportValidationFailure(TestWorkspace w)
{
    var target = Path.Combine(w.Root, "existing.zip");
    var original = Encoding.UTF8.GetBytes("keep the existing destination");
    File.WriteAllBytes(target, original);
    var catalog = new SkinCatalog(w.Source, w.Managed);
    w.Frame("Idle/idle.png"); w.Frame("Happy/bad.png", [1, 2, 3]);
    Reject(() => Export(catalog, "default", target));
    Check(File.ReadAllBytes(target).SequenceEqual(original), "invalid PNG destroyed existing export");
    File.Delete(Path.Combine(w.Source, "Happy/bad.png"));
    w.Write("mouth-anchors.json", new byte[128 * 1024 + 1]);
    Reject(() => Export(catalog, "default", target), "anchor");
    Check(File.ReadAllBytes(target).SequenceEqual(original), "oversized anchors destroyed existing export");
    File.Delete(Path.Combine(w.Source, "mouth-anchors.json"));
    using (var stream = File.OpenWrite(Path.Combine(w.Source, "Idle/idle.png"))) stream.SetLength(8 * 1024 * 1024 + 1);
    Reject(() => Export(catalog, "default", target), "frame");
    Check(File.ReadAllBytes(target).SequenceEqual(original), "oversized frame destroyed existing export");
    Equal(1, Directory.EnumerateFiles(w.Root).Count());
}
static void ExportReplacementFailure(TestWorkspace w)
{
    w.Frame("Idle/idle.png");
    var target = Path.Combine(w.Root, "locked.zip");
    var original = Encoding.UTF8.GetBytes("keep locked export");
    File.WriteAllBytes(target, original);
    using (var locked = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.Read))
    {
        try { Export(new SkinCatalog(w.Source, w.Managed), "default", target); throw new Exception("locked export unexpectedly succeeded"); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
    }
    Check(File.ReadAllBytes(target).SequenceEqual(original), "failed replacement destroyed target");
    Equal(1, Directory.EnumerateFiles(w.Root).Count());
}
static void ExportSourceCollision(TestWorkspace w)
{
    var source = w.Frame("Idle/idle.png");
    var imported = w.Catalog().ImportFolder(w.Source, "Keep source");
    Reject(() => Export(w.Catalog(), imported.Id, imported.PreviewPath!), "source");
    Reject(() => Export(w.Catalog(), imported.Id, Path.Combine(imported.RootPath, "new.zip")), "source");
    Reject(() => Export(new SkinCatalog(w.Source, w.Managed), "default", source), "source");
    Check(File.ReadAllBytes(source).SequenceEqual(TestWorkspace.Png()), "export overwrote a source frame");
    Check(File.ReadAllBytes(imported.PreviewPath!).SequenceEqual(TestWorkspace.Png()), "export overwrote an installed frame");
    Equal(imported.Id, w.Catalog().Resolve(imported.Id).Id);
}
static void ExportExtendedSourceCollision(TestWorkspace w)
{
    var source = w.Frame("Idle/idle.png");
    var catalog = new SkinCatalog(w.Source, w.Managed);
    Reject(() => Export(catalog, "default", @"\\?\" + source), "source");
    Reject(() => Export(new SkinCatalog(@"\\?\" + w.Source, w.Managed), "default", source), "source");
    Check(File.ReadAllBytes(source).SequenceEqual(TestWorkspace.Png()), "extended path export overwrote its source frame");
    var destination = Path.Combine(w.Root, "extended.zip");
    Export(catalog, "default", @"\\?\" + destination);
    var imported = catalog.ImportZip(destination);
    Equal("默认比熊", imported.Name);
    Check(!File.Exists(Path.Combine(imported.RootPath, "mouth-anchors.json")), "an absent optional anchor file must remain absent after roundtrip");
}
static void ExportWrongSuffix(TestWorkspace w)
{
    w.Frame("Idle/idle.png");
    var target = Path.Combine(w.Root, "existing.json");
    var original = Encoding.UTF8.GetBytes("{\"preserve\":\"existing data\"}");
    File.WriteAllBytes(target, original);
    Reject(() => Export(new SkinCatalog(w.Source, w.Managed), "default", target), "zip");
    Check(File.ReadAllBytes(target).SequenceEqual(original), "wrong-suffix export overwrote existing data");
    Equal(1, Directory.EnumerateFiles(w.Root).Count());
}
static void ExportLinks(TestWorkspace w)
{
    var link = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Application Data");
    w.Frame("Idle/idle.png");
    Reject(() => Export(new SkinCatalog(link, w.Managed), "default", Path.Combine(w.Root, "linked-source.zip")));
    Reject(() => Export(new SkinCatalog(w.Source, w.Managed), "default", Path.Combine(link, "unwritten-skin-export.zip")));
    Equal(0, Directory.EnumerateFiles(w.Root).Count());
}
static void Export(SkinCatalog catalog, string id, string destination)
{
    catalog.ExportZip(id, destination);
}
static void ZipRoot(TestWorkspace w)
{
    var zip = w.Zip(("Idle/frame.png", TestWorkspace.Png()), ("skin.json", Encoding.UTF8.GetBytes("{\"displayName\":\"Zip pet\"}")));
    var info = w.Catalog().ImportZip(zip);
    Equal("Zip pet", info.Name);
    Check(File.Exists(info.PreviewPath), "zip preview must be managed");
    File.Delete(zip);
    Equal(info.Id, w.Catalog().Resolve(info.Id).Id);
}
static void ZipWrapped(TestWorkspace w)
{
    var zip = w.Zip(("MyPet/", []), ("MyPet/Idle/", []), ("MyPet/Idle/idle.png", TestWorkspace.Png()));
    var info = w.Catalog().ImportZip(zip, "Wrapped");
    Equal("Wrapped", info.Name);
    Equal("Idle", Path.GetFileName(Path.GetDirectoryName(info.PreviewPath)));
}
static void AtomicFailure(TestWorkspace w)
{
    w.Frame("Idle/good.png"); w.Frame("Happy/bad.png", [1, 2, 3]);
    Reject(() => w.Catalog().ImportFolder(w.Source));
    Equal(1, w.Catalog().List().Count);
    Equal(0, Directory.EnumerateFileSystemEntries(w.Managed).Count());
}
static void RequiredIdle(TestWorkspace w)
{
    w.Frame("Happy/happy.png"); Reject(() => w.Catalog().ImportFolder(w.Source));
    Directory.CreateDirectory(Path.Combine(w.Source, "Idle"));
    Reject(() => w.Catalog().ImportFolder(w.Source));
}
static void MalformedPng(TestWorkspace w)
{
    w.Frame("Idle/idle.png", Encoding.UTF8.GetBytes("not a PNG"));
    Reject(() => w.Catalog().ImportFolder(w.Source));
}
static void PngDimensions(TestWorkspace w)
{
    foreach (var (width, height) in new[] { (31, 32), (32, 31), (2049, 32), (32, 2049) })
    {
        w.Frame("Idle/idle.png", TestWorkspace.Png(width, height));
        Reject(() => w.Catalog().ImportFolder(w.Source));
    }
    w.Frame("Idle/idle.png", TestWorkspace.Png(32, 2048));
    Check(w.Catalog().ImportFolder(w.Source).PreviewPath is not null, "dimension boundary must be accepted");
}
static void PngHeader(TestWorkspace w)
{
    var invalidDepth = TestWorkspace.Png(); invalidDepth[24] = 3;
    w.Frame("Idle/idle.png", invalidDepth); Reject(() => w.Catalog().ImportFolder(w.Source));
    var badCrc = TestWorkspace.Png(); badCrc[29] ^= 1;
    w.Frame("Idle/idle.png", badCrc); Reject(() => w.Catalog().ImportFolder(w.Source));
}
static void TruncatedPng(TestWorkspace w)
{
    w.Frame("Idle/idle.png", TestWorkspace.Png()[..33]);
    Reject(() => w.Catalog().ImportFolder(w.Source));
}
static void BadManifest(TestWorkspace w)
{
    w.Frame("Idle/idle.png");
    foreach (var manifest in new[] { "{", "[]", "{\"name\":12}", "{\"name\":\"\\n\"}" })
    {
        File.WriteAllText(Path.Combine(w.Source, "skin.json"), manifest);
        Reject(() => w.Catalog().ImportFolder(w.Source));
    }
}
static void UnknownPayload(TestWorkspace w)
{
    foreach (var name in new[] { "plugin.exe", "readme.txt", "pet.jpg", "Idle/plugin.dll", "Idle/nested/frame.png", "Unknown/frame.png" })
    {
        using var item = new TestWorkspace();
        item.Frame("Idle/idle.png"); item.Write(name, TestWorkspace.Png());
        Reject(() => item.Catalog().ImportFolder(item.Source));
    }
}
static void ZipPaths(TestWorkspace w)
{
    foreach (var name in new[] { "../escaped.png", "/root.png", "C:/absolute.png", "Idle/../../escape.png", "Idle\\..\\escape.png", "Idle/CON.png", "Idle/with:stream.png", "Idle/frame.png.", "Idle//empty.png" })
    {
        var zip = w.Zip(("Idle/idle.png", TestWorkspace.Png()), (name, TestWorkspace.Png()));
        Reject(() => w.Catalog().ImportZip(zip));
        Equal(1, w.Catalog().List().Count);
    }
}
static void ZipLinks(TestWorkspace w)
{
    var zip = w.Zip(("Idle/idle.png", TestWorkspace.Png()));
    using (var archive = ZipFile.Open(zip, ZipArchiveMode.Update))
    {
        var entry = archive.CreateEntry("Idle/link.png");
        entry.ExternalAttributes = unchecked((int)(0xA1FFu << 16));
        using var writer = new StreamWriter(entry.Open()); writer.Write("../../../outside");
    }
    Reject(() => w.Catalog().ImportZip(zip));
}
static void ZipDuplicates(TestWorkspace w)
{
    var zip = w.Zip(("Idle/frame.png", TestWorkspace.Png()), ("idle/FRAME.png", TestWorkspace.Png()));
    Reject(() => w.Catalog().ImportZip(zip));
}
static void FolderLinks(TestWorkspace w)
{
    // Existing Windows compatibility junction: the importer must reject it before any enumeration.
    var link = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Application Data");
    Check((File.GetAttributes(link) & FileAttributes.ReparsePoint) != 0, "Windows junction fixture unavailable");
    Reject(() => w.Catalog().ImportFolder(link));
}
static void ManagedRootLinks(TestWorkspace w)
{
    var link = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Application Data");
    w.Frame("Idle/idle.png");
    Reject(() => new SkinCatalog(w.Default, link).ImportFolder(w.Source));
}
static void FrameSize(TestWorkspace w)
{
    var path = w.Frame("Idle/large.png");
    using (var stream = File.OpenWrite(path)) stream.SetLength(8 * 1024 * 1024 + 1);
    Reject(() => w.Catalog().ImportFolder(w.Source), "frame");
    var zip = w.Zip(("Idle/large.png", new byte[8 * 1024 * 1024 + 1]));
    Reject(() => w.Catalog().ImportZip(zip), "frame");
    Reject(() => Export(new SkinCatalog(w.Source, w.Managed), "default", Path.Combine(w.Root, "oversized-frame-export.zip")), "frame");
}
static void TotalSize(TestWorkspace w)
{
    for (var i = 0; i < 17; i++)
    {
        var path = w.Frame($"Idle/large{i}.png");
        using var stream = File.OpenWrite(path); stream.SetLength(8 * 1024 * 1024);
    }
    Reject(() => w.Catalog().ImportFolder(w.Source), "total");
    var zip = Path.Combine(w.Root, "total.zip");
    using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
    {
        var bytes = new byte[8 * 1024 * 1024];
        for (var i = 0; i < 17; i++) { using var output = archive.CreateEntry($"Idle/large{i}.png").Open(); output.Write(bytes); }
    }
    Reject(() => w.Catalog().ImportZip(zip), "total");
    Reject(() => Export(new SkinCatalog(w.Source, w.Managed), "default", Path.Combine(w.Root, "oversized-total-export.zip")), "total");
}
static void FrameCount(TestWorkspace w)
{
    var png = TestWorkspace.Png();
    for (var i = 0; i < 5001; i++) w.Frame($"Idle/frame{i}.png", png);
    Reject(() => w.Catalog().ImportFolder(w.Source), "frame");
    var zip = Path.Combine(w.Root, "count.zip");
    using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
        for (var i = 0; i < 5001; i++) { using var output = archive.CreateEntry($"Idle/frame{i}.png").Open(); output.Write(png); }
    Reject(() => w.Catalog().ImportZip(zip), "frame");
    Reject(() => Export(new SkinCatalog(w.Source, w.Managed), "default", Path.Combine(w.Root, "oversized-count-export.zip")), "frame");
}
static void InstalledTotalSize(TestWorkspace w)
{
    var png = TestWorkspace.PngWithPadding(8 * 1024 * 1024);
    for (var i = 0; i < 16; i++) w.Frame($"Idle/frame{i}.png", png);
    Reject(() => w.Catalog().ImportFolder(w.Source), "total");
    Equal(1, w.Catalog().List().Count);
    Reject(() => Export(new SkinCatalog(w.Source, w.Managed), "default", Path.Combine(w.Root, "metadata-total-export.zip")), "total");
}
static void ManifestSize(TestWorkspace w)
{
    w.Frame("Idle/idle.png");
    var metadata = Encoding.UTF8.GetBytes("{\"ignored\":\"" + new string('a', 65536) + "\"}");
    w.Write("skin.json", metadata);
    Reject(() => w.Catalog().ImportFolder(w.Source), "manifest");
    Reject(() => w.Catalog().ImportZip(w.Zip(("Idle/idle.png", TestWorkspace.Png()), ("skin.json", metadata))), "manifest");
}
static void DecodedPixels(TestWorkspace w)
{
    var large = TestWorkspace.Png(2048, 2048);
    for (var i = 0; i < 17; i++) w.Frame($"Idle/frame{i}.png", large);
    Reject(() => w.Catalog().ImportFolder(w.Source), "pixel");
    var entries = Enumerable.Range(0, 17).Select(i => ($"Idle/frame{i}.png", large)).ToArray();
    Reject(() => w.Catalog().ImportZip(w.Zip(entries)), "pixel");
    Equal(1, w.Catalog().List().Count);
    Reject(() => Export(new SkinCatalog(w.Source, w.Managed), "default", Path.Combine(w.Root, "oversized-pixel-export.zip")), "pixel");
}
static void CatalogCorruption(TestWorkspace w)
{
    w.Frame("Idle/idle.png"); var info = w.Catalog().ImportFolder(w.Source);
    File.WriteAllText(Path.Combine(info.RootPath, "skin.json"), "bad");
    var logs = new List<string>(); var catalog = new SkinCatalog(w.Default, w.Managed, logs.Add);
    Equal(1, catalog.List().Count); Equal("default", catalog.Resolve(info.Id).Id);
    Check(logs.Count > 0, "skipped imported skin must be logged");
    var staging = Path.Combine(w.Managed, ".import-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(staging); Equal(1, catalog.List().Count);
}
static void CatalogOrder(TestWorkspace w)
{
    w.Frame("Idle/idle.png");
    w.Catalog().ImportFolder(w.Source, "Zebra"); w.Catalog().ImportFolder(w.Source, "Alpha");
    var list = w.Catalog().List();
    Equal("default", list[0].Id); Equal("Alpha", list[1].Name); Equal("Zebra", list[2].Name);
}
static void Reject(Action action, string? contains = null)
{
    try { action(); }
    catch (InvalidDataException error)
    {
        if (contains is not null) Check(error.Message.Contains(contains, StringComparison.OrdinalIgnoreCase), $"expected diagnostic containing {contains}, got: {error.Message}");
        return;
    }
    throw new Exception("unsafe import was accepted");
}
static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
static void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}, got {actual}"); }

sealed class TestWorkspace : IDisposable
{
    private static readonly List<string> PendingCleanup = [];
    public string Root { get; } = Path.Combine(AppContext.BaseDirectory, "skin-test-" + Guid.NewGuid().ToString("N"));
    public string Default => Path.Combine(Root, "default");
    public string Managed => Path.Combine(Root, "managed");
    public string Source => Path.Combine(Root, "source");
    public TestWorkspace() { Directory.CreateDirectory(Default); Directory.CreateDirectory(Managed); Directory.CreateDirectory(Source); }
    public SkinCatalog Catalog() => new(Default, Managed);
    public string Write(string relative, byte[] bytes) { var path = Path.Combine(Source, relative); Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllBytes(path, bytes); return path; }
    public string Frame(string relative, byte[]? bytes = null) => Write(relative, bytes ?? Png());
    public string Zip(params (string Name, byte[] Bytes)[] entries)
    {
        var path = Path.Combine(Root, Guid.NewGuid().ToString("N") + ".zip");
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (name, bytes) in entries) { using var output = archive.CreateEntry(name).Open(); output.Write(bytes); }
        return path;
    }
    public void DeleteSource() => Directory.Delete(Source, true);
    public void Dispose()
    {
        var prefix = Path.GetFullPath(AppContext.BaseDirectory) + "skin-test-";
        if (!Path.GetFullPath(Root).StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new Exception("test cleanup escaped owned output directory");
        for (var attempt = 0; ; attempt++)
        {
            try { Directory.Delete(Root, true); break; }
            catch (IOException) when (attempt < 20) { Thread.Sleep(100); }
            catch (IOException) { PendingCleanup.Add(Root); break; }
        }
    }
    public static bool CleanupPending()
    {
        for (var attempt = 0; attempt < 100 && PendingCleanup.Count > 0; attempt++)
        {
            foreach (var root in PendingCleanup.ToArray())
            {
                try { if (Directory.Exists(root)) Directory.Delete(root, true); PendingCleanup.Remove(root); }
                catch (IOException) { }
            }
            if (PendingCleanup.Count > 0) Thread.Sleep(100);
        }
        foreach (var root in PendingCleanup) Console.WriteLine($"Fixture cleanup incomplete (external file lock): {root}");
        return PendingCleanup.Count == 0;
    }
    public static byte[] Png(int width = 32, int height = 32)
    {
        using var output = new MemoryStream(); output.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        var header = new byte[13]; BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(0, 4), (uint)width); BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4, 4), (uint)height); header[8] = 8; header[9] = 6;
        Chunk(output, "IHDR", header);
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.SmallestSize, true)) zlib.Write(new byte[height * (width * 4 + 1)]);
        Chunk(output, "IDAT", compressed.ToArray()); Chunk(output, "IEND", []);
        return output.ToArray();
    }
    public static byte[] PngWithPadding(int bytes)
    {
        var png = Png(); using var output = new MemoryStream();
        output.Write(png.AsSpan(0, png.Length - 12));
        Chunk(output, "npAD", new byte[bytes - png.Length - 12]);
        output.Write(png.AsSpan(png.Length - 12)); return output.ToArray();
    }
    private static void Chunk(Stream output, string kind, byte[] data)
    {
        var type = Encoding.ASCII.GetBytes(kind); Span<byte> value = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(value, (uint)data.Length); output.Write(value); output.Write(type); output.Write(data);
        var crc = 0xFFFFFFFFu;
        foreach (var item in type.Concat(data)) { crc ^= item; for (var i = 0; i < 8; i++) crc = (crc >> 1) ^ ((crc & 1) == 1 ? 0xEDB88320u : 0); }
        BinaryPrimitives.WriteUInt32BigEndian(value, ~crc); output.Write(value);
    }
}
