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
            T("• Discord shows Main Menu · Idle 🌙 on the launcher home screen. In Minecraft, it shows the version and local name when available.\n\n• The status area shows the selected theme.\n\n• The RAM badge uses an alternate measurement when Windows cannot report private resident memory.\n\n• Update notes follow the launcher language and can be scrolled.",
              "• Discord, ana ekranda Ana Menü · Boşta 🌙 durumunu gösterir. Minecraft’ta varsa sürümü ve yerel adı gösterir.\n\n• Seçilen tema sol alttaki durum alanında görünür.\n\n• Windows özel bellek ölçümünü veremezse RAM göstergesi toplam fiziksel bellek kullanımını gösterir.\n\n• Güncelleme notları arayüz diline göre gösterilir ve kaydırılabilir."));
    }
}
