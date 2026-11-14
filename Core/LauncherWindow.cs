using Microsoft.Win32;

namespace KoklukBedrockModloader;

public sealed partial class MainForm
{
    NotifyIcon? trayIcon;
    bool exitRequested;

    void CloseToDesktop()
    {
        exitRequested = true;
        Close();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        if (!e.Cancel && backupBusy && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            exitRequested = false;
            UpdateStatus(T("Wait for the backup to finish.", "Yedek işleminin bitmesini bekle."));
            return;
        }
        if (e.Cancel || exitRequested || e.CloseReason != CloseReason.UserClosing || !settings.CloseToTray) return;
        e.Cancel = true;
        if (trayIcon == null)
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add(T("Open BedrockLoader", "BedrockLoader’ı aç"), null, (_, _) => RestoreFromTray());
            menu.Items.Add(T("Exit", "Çıkış"), null, (_, _) => CloseToDesktop());
            trayIcon = new NotifyIcon { Text = "BedrockLoader", Icon = Icon ?? SystemIcons.Application, ContextMenuStrip = menu };
            trayIcon.DoubleClick += (_, _) => RestoreFromTray();
        }
        trayIcon.Visible = true;
        Hide();
        if (!settings.TrayNoticeShown)
        {
            trayIcon.ShowBalloonTip(4500, "BedrockLoader",
                T("Still running in the system tray. Double-click the icon to reopen.",
                    "Sistem tepsisinde çalışmaya devam ediyorum. Geri dönmek için simgeme çift tıkla."), ToolTipIcon.Info);
            settings.TrayNoticeShown = true;
            SaveSettings();
        }
    }

    void RestoreFromTray()
    {
        DisposeTray();
        if (!Visible) Show();
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        Activate();
        RefreshDownloadTaskbar();
    }

    void DisposeTray()
    {
        if (trayIcon == null) return;
        trayIcon.Visible = false;
        trayIcon.ContextMenuStrip?.Dispose();
        trayIcon.Dispose();
        trayIcon = null;
    }

    static bool WindowsUsesLightTheme() =>
        Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
            "AppsUseLightTheme", 1) is not 0;
}
