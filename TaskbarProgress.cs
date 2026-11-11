using System.Runtime.InteropServices;

namespace KoklukBedrockModloader;

public sealed partial class MainForm
{
    [ComImport, Guid("EA1AFB91-9E28-4B86-90E9-9E9F8A5EEFAF"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface ITaskbarList3
    {
        void HrInit();
        void AddTab(nint window);
        void DeleteTab(nint window);
        void ActivateTab(nint window);
        void SetActiveAlt(nint window);
        void MarkFullscreenWindow(nint window, [MarshalAs(UnmanagedType.Bool)] bool fullscreen);
        void SetProgressValue(nint window, ulong completed, ulong total);
        void SetProgressState(nint window, uint state);
    }

    ITaskbarList3? taskbar;
    bool minecraftTaskbarBusy;

    void RefreshDownloadTaskbar()
    {
        if (!IsHandleCreated || IsDisposed) return;
        if (taskbar == null && cinnabarPercent < 0 && !minecraftTaskbarBusy) return;
        try
        {
            if (taskbar == null)
            {
                var type = Type.GetTypeFromCLSID(new Guid("56FDF344-FD6D-11D0-958A-006097C9A090"), true)!;
                taskbar = (ITaskbarList3)Activator.CreateInstance(type)!;
                taskbar.HrInit();
            }
            if (cinnabarPercent >= 0)
            {
                taskbar.SetProgressState(Handle, 2);
                taskbar.SetProgressValue(Handle, (ulong)cinnabarPercent, 100);
            }
            else taskbar.SetProgressState(Handle, minecraftTaskbarBusy ? 1u : 0u);
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException) { taskbar = null; }
    }
}
