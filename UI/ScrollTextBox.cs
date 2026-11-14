namespace KoklukBedrockModloader;

public sealed partial class MainForm
{
    // Keep native text selection, copying and keyboard navigation without
    // drawing Windows' bright scrollbar over the application's palette.
    sealed class ScrollTextBox : TextBox
    {
        int wheelRemainder;

        public ScrollTextBox()
        {
            Multiline = true; ReadOnly = true; WordWrap = true;
            ScrollBars = ScrollBars.None; BorderStyle = BorderStyle.None;
        }

        protected override void WndProc(ref Message message)
        {
            const int WmMouseWheel = 0x020A, EmLineScroll = 0x00B6;
            if (message.Msg == WmMouseWheel && Multiline)
            {
                wheelRemainder += unchecked((short)((long)message.WParam >> 16));
                int steps = wheelRemainder / 120;
                wheelRemainder %= 120;
                int lines = SystemInformation.MouseWheelScrollLines;
                if (lines < 0) lines = Math.Max(1, ClientSize.Height / Math.Max(1, Font.Height));
                if (steps != 0 && lines > 0) SendMessage(Handle, EmLineScroll, IntPtr.Zero, (IntPtr)(-steps * lines));
                message.Result = IntPtr.Zero;
                return;
            }
            base.WndProc(ref message);
        }
    }
}
