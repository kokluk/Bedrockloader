using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace KoklukBedrockModloader;

public sealed partial class MainForm
{
    LauncherUsageBadge? usageBadge;

    sealed class LauncherUsageBadge : Control
    {
        readonly MainForm owner;
        readonly Process process = Process.GetCurrentProcess();
        readonly System.Windows.Forms.Timer timer = new() { Interval = 1000 };
        readonly Font badgeFont = new("Segoe UI", 9, FontStyle.Bold);
        double? memoryMiB;

        [StructLayout(LayoutKind.Sequential)]
        struct ProcessMemoryCounters
        {
            public uint Size, PageFaultCount;
            public nuint PeakWorkingSet, WorkingSet, PeakPagedPool, PagedPool, PeakNonPagedPool, NonPagedPool,
                PagefileUsage, PeakPagefileUsage, PrivateUsage, PrivateWorkingSet;
            public ulong SharedCommitUsage;
        }

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool K32GetProcessMemoryInfo(IntPtr handle, ref ProcessMemoryCounters counters, uint size);

        public LauncherUsageBadge(MainForm owner)
        {
            this.owner = owner;
            Size = new Size(116, 31);
            AccessibleName = owner.T("BedrockLoader resource usage", "BedrockLoader kaynak kullanımı");
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            timer.Tick += (_, _) => Sample();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible && Parent != null && !IsDisposed)
            {
                Sample();
                timer.Start();
            }
            else
            {
                timer.Stop();
            }
        }

        void Sample()
        {
            var counters = new ProcessMemoryCounters { Size = (uint)Marshal.SizeOf<ProcessMemoryCounters>() };
            if (K32GetProcessMemoryInfo(process.Handle, ref counters, counters.Size) && counters.PrivateWorkingSet > 0)
                memoryMiB = (double)counters.PrivateWorkingSet / 1048576d;
            else
            {
                process.Refresh();
                memoryMiB = process.WorkingSet64 / 1048576d;
            }
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent?.BackColor ?? owner.Surface);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int pillWidth = Math.Max(1, Width);
            DrawPill(0, memoryMiB.HasValue ? $"RAM {memoryMiB:0.0} MB" : "RAM …", memoryMiB ?? 0, 384, 768);

            void DrawPill(int x, string label, double value, double warning, double high)
            {
                Color signal = value >= high ? Color.FromArgb(239, 83, 95) : value >= warning
                    ? Color.FromArgb(233, 154, 61) : Color.FromArgb(76, 189, 130);
                Color background = MixColors(owner.SurfaceAlt, signal, owner.lightMode ? .25 : .22);
                Color foreground = MixColors(signal, owner.lightMode ? Color.Black : Color.White, owner.lightMode ? .60 : .38);
                using var brush = new SolidBrush(background);
                using var path = SoftIcons.Round(x, 0, pillWidth, Height - 1, 15);
                e.Graphics.FillPath(brush, path);
                TextRenderer.DrawText(e.Graphics, label, badgeFont, new Rectangle(x + 4, 0, Math.Max(0, pillWidth - 8), Height), foreground,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                timer.Stop();
                process.Dispose();
                badgeFont.Dispose();
            }
            base.Dispose(disposing);
            if (disposing) timer.Dispose();
        }
    }
}
