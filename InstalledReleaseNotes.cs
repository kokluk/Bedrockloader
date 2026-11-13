namespace KoklukBedrockModloader;

public sealed partial class MainForm
{
    static bool IsVersionUpgrade(string? previous, Version current)
    {
        if (string.IsNullOrWhiteSpace(previous)) return false;
        try { return ReleaseUpdates.ParseVersion(previous) < current; }
        catch (InvalidDataException) { return false; }
    }

    void ShowInstalledReleaseNotes()
    {
        var current = ReleaseUpdates.CurrentVersion;
        string marker = Path.Combine(appDataRoot, "PreviousInstalledVersion.txt");
        string? previousInstalled = null;
        try { if (File.Exists(marker)) previousInstalled = File.ReadAllText(marker); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }

        bool upgraded = IsVersionUpgrade(settings.LastRunVersion, current) ||
            (settings.LastRunVersion != current.ToString() && IsVersionUpgrade(previousInstalled, current));
        if (settings.LastRunVersion != current.ToString())
        {
            settings.LastRunVersion = current.ToString();
            SaveSettings();
        }
        try { if (previousInstalled != null) File.Delete(marker); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }

        if (!upgraded) return;
        ShowThemedPopup(T($"What's new in {DisplayVersion}", $"{DisplayVersion} sürümündeki yenilikler"),
            T("• Cinnabar has a dedicated WASM mods page.\n\n• Logs and available crash reports can be exported as a ZIP.\n\n• The optional RAM indicator is now visible on every page.",
              "• Cinnabar için ayrı WASM modları sayfası eklendi.\n\n• Kayıtlar ve mevcut çökme raporları ZIP olarak dışa aktarılabiliyor.\n\n• İsteğe bağlı RAM göstergesi artık tüm sayfalarda görünüyor."));
    }
}
