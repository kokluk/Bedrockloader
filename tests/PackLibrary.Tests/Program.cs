using KoklukBedrockModloader;

int passed = 0;
void Assert(bool condition, string name) { if (!condition) throw new Exception(name); passed++; Console.WriteLine("PASS " + name); }
void Reject(Action action, string name)
{
    try { action(); } catch (ArgumentException) { passed++; Console.WriteLine("PASS " + name); return; }
    throw new Exception("Expected rejection: " + name);
}
string temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
string root = Path.GetFullPath(Path.Combine(temp, "bedrock-pack-tests-" + Guid.NewGuid().ToString("N")));
if (!root.StartsWith(temp, StringComparison.OrdinalIgnoreCase) || Directory.Exists(root)) throw new Exception("Unsafe test fixture path.");
Directory.CreateDirectory(root);
try
{
    Directory.CreateDirectory(Path.Combine(root, "resource_packs", "Shared"));
    Directory.CreateDirectory(Path.Combine(root, "resource_packs", "Alpha"));
    Directory.CreateDirectory(Path.Combine(root, "resource_packs", "§nVibrant Visuals§r"));
    Directory.CreateDirectory(Path.Combine(root, "behavior_packs", "Shared"));
    Directory.CreateDirectory(Path.Combine(root, "skin_packs", "Other"));
    var resources = PackLibrary.Read(root, "resource_packs").ToArray();
    var behaviors = PackLibrary.Read(root, "behavior_packs").ToArray();
    Assert(resources.Length == 3 && resources.All(x => x.Category == "resource_packs"), "resource column contains only resource packs");
    Assert(behaviors.Length == 1 && behaviors.All(x => x.Category == "behavior_packs"), "behavior column contains only behavior packs");
    Assert(resources[0].DirectoryName == "Alpha", "pack names are sorted");
    Assert(!resources.Any(x => x.DirectoryName == "Other"), "other pack types are not misclassified");
    Assert(PackLibrary.Read(Path.Combine(root, "missing"), "resource_packs").Count() == 0, "missing folder produces an empty column");
    var sharedResource = resources.Single(x => x.DirectoryName == "Shared");
    Assert(PackLibrary.ResolvePath(root, sharedResource) != PackLibrary.ResolvePath(root, behaviors[0]), "identically named packs retain their own category");
    var styled = resources.Single(x => x.DirectoryName.Contains('§'));
    Assert(PackLibrary.ResolvePath(root, styled) == Path.Combine(root, "resource_packs", "§nVibrant Visuals§r"), "formatted pack name preserves its actual folder");
    foreach (string invalid in new[] { "..", "../Shared", "..\\Shared", "C:\\outside", "", "resource_packs/Shared" })
        Reject(() => PackLibrary.ResolvePath(root, new InstalledPack("behavior_packs", invalid)), "unsafe folder rejected: " + invalid);
    Reject(() => PackLibrary.ResolvePath(root, new InstalledPack("../outside", "Shared")), "invalid category cannot be used for deletion");
    Assert(Directory.Exists(PackLibrary.ResolvePath(root, sharedResource)) && Directory.Exists(PackLibrary.ResolvePath(root, behaviors[0])), "path checks leave packs untouched");
}
finally
{
    // This unique fixture was created above, and its absolute path is confined
    // to the operating system's temporary directory before recursive cleanup.
    if (!Path.GetFullPath(root).StartsWith(temp, StringComparison.OrdinalIgnoreCase)) throw new Exception("Unsafe cleanup path.");
    Directory.Delete(root, recursive: true);
}
Console.WriteLine($"{passed} checks passed. User pack folders were not accessed.");
