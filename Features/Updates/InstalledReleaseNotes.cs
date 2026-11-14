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
            T("• AMOLED now offers the Material You color palette with a pure-black background in dark mode.",
              "• AMOLED artık Material You renk paletini sunuyor. Koyu modda arka plan tam siyah kalıyor."));
    }
}
