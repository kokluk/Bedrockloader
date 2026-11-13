using System.ComponentModel;
using System.Diagnostics;

namespace KoklukBedrockModloader;

public sealed partial class MainForm
{
    bool deletingLauncherData;

    bool ConfirmLauncherRemoval(string title, string explanation, string action) =>
        ShowThemedPopup(title, explanation + "\n\n" + T("Are you sure?", "Emin misin?"),
            true, T("Continue", "Devam et"), destructive: true) == DialogResult.Yes &&
        ShowThemedPopup(T("Final confirmation", "Son onay"), explanation + "\n\n" +
            T("Are you sure you want to continue? This action cannot be undone.",
                "Devam etmek istediğinden emin misin? Bu işlem geri alınamaz."),
            true, action, destructive: true) == DialogResult.Yes;

    bool CanRemoveLauncher()
    {
        if (!backupBusy && !updatesBusy && !cinnabarBusy && !installingModloader) return true;
        ShowThemedPopup(T("Operation in progress", "İşlem sürüyor"),
            T("Wait for the current installation, update, or backup to finish.",
                "Devam eden kurulum, güncelleme veya yedekleme işleminin bitmesini bekle."));
        return false;
    }

    void DeleteLauncherData()
    {
        string title = T("Delete app data", "Uygulama verilerini sil");
        string details = T("Deletes BedrockLoader preferences, saved servers, logs, local skin library and other files in the launcher data folder. Launcher-managed Cinnabar installations in that folder are also deleted. Minecraft worlds, Minecraft packs and the separate local mods folder are kept. The launcher will close.",
            "BedrockLoader ayarları, kayıtlı sunucular, günlükler, yerel skin kütüphanesi ve launcher veri klasöründeki diğer dosyalar silinir. Bu klasördeki Cinnabar kurulumları da silinir. Minecraft dünyaları, Minecraft paketleri ve ayrı yerel mod klasörü korunur. Launcher kapanır.");
        if (!CanRemoveLauncher() || !ConfirmLauncherRemoval(title, details, T("Delete data", "Verileri sil")) || !CanRemoveLauncher()) return;
        try
        {
            lock (logLock)
            {
                deletingLauncherData = true;
                if (Directory.Exists(appDataRoot))
                {
                    if (IsReparsePoint(appDataRoot)) throw new IOException(T("The launcher data folder is a link.", "Launcher veri klasörü bir bağlantı."));
                    Directory.Delete(appDataRoot, recursive: true);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            lock (logLock) deletingLauncherData = false;
            ShowThemedPopup(T("Data removal incomplete", "Veri silme tamamlanamadı"), ex.Message);
            return;
        }
        CloseToDesktop();
    }

    void UninstallLauncher()
    {
        string title = T("Uninstall app", "Uygulamayı sil");
        string uninstaller = Path.Combine(AppContext.BaseDirectory, "unins000.exe");
        if (!File.Exists(uninstaller) || !File.Exists(Path.ChangeExtension(uninstaller, ".dat")))
        {
            ShowThemedPopup(title, T("This copy has no installer removal tool. If you installed BedrockLoader, uninstall it from Windows Settings → Apps.",
                "Bu kopyada kaldırma aracı bulunamadı. BedrockLoader’ı kurduysan Windows Ayarları → Uygulamalar üzerinden kaldırabilirsin."));
            return;
        }
        string details = T("Removes the installed launcher and its shortcuts. App data, local mods and Minecraft files are kept.",
            "Kurulu launcher ve kısayolları kaldırılır. Uygulama verileri, yerel modlar ve Minecraft dosyaları korunur.");
        if (!CanRemoveLauncher() || !ConfirmLauncherRemoval(title, details, T("Uninstall", "Uygulamayı sil")) || !CanRemoveLauncher()) return;
        try
        {
            var start = new ProcessStartInfo(uninstaller) { UseShellExecute = true, WorkingDirectory = AppContext.BaseDirectory };
            start.ArgumentList.Add("/SILENT");
            start.ArgumentList.Add("/SUPPRESSMSGBOXES");
            start.ArgumentList.Add("/NORESTART");
            using var process = Process.Start(start) ?? throw new IOException(T("Could not start the uninstaller.", "Kaldırma aracı başlatılamadı."));
            CloseToDesktop();
        }
        catch (Exception ex) when (ex is Win32Exception or IOException or UnauthorizedAccessException)
        {
            ShowThemedPopup(T("Uninstall failed", "Kaldırma başarısız"), ex.Message);
        }
    }
}
