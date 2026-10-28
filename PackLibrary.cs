namespace KoklukBedrockModloader;

internal sealed record InstalledPack(string Category, string DirectoryName)
{
    public override string ToString() => DirectoryName;
}

internal static class PackLibrary
{
    static void ValidateCategory(string category)
    {
        if (category is not ("resource_packs" or "behavior_packs")) throw new ArgumentException("Unsupported pack category.");
    }

    public static IEnumerable<InstalledPack> Read(string root, string category)
    {
        ValidateCategory(category);
        string folder = Path.Combine(root, category);
        return Directory.Exists(folder)
            ? Directory.GetDirectories(folder).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).Select(path => new InstalledPack(category, Path.GetFileName(path))).ToArray()
            : Array.Empty<InstalledPack>();
    }

    public static string ResolvePath(string root, InstalledPack pack)
    {
        ValidateCategory(pack.Category);
        if (string.IsNullOrWhiteSpace(pack.DirectoryName) || pack.DirectoryName is "." or ".." || pack.DirectoryName.IndexOfAny(new[] { '/', '\\', ':' }) >= 0)
            throw new ArgumentException("Invalid pack folder name.");
        string categoryRoot = Path.GetFullPath(Path.Combine(root, pack.Category)).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string path = Path.GetFullPath(Path.Combine(categoryRoot, pack.DirectoryName));
        if (!path.StartsWith(categoryRoot, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Pack path is outside its category.");
        return path;
    }
}
