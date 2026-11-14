namespace KoklukBedrockModloader;

public sealed partial class MainForm
{
    RoundedPanel BuildCinnabarDropArea(string title, string description, string fileFilter,
        Func<string, bool> acceptsFile, Action<string[]> import)
    {
        var card = new RoundedPanel { Dock = DockStyle.Fill, CornerRadius = 20, Tag = "card",
            Padding = new Padding(16, 10, 16, 12), Margin = new Padding(0, 0, 0, 12) };
        var heading = WrappedText(title, 11, true);
        heading.Height = 24;
        var subtitle = WrappedText(description);
        subtitle.Height = 26;
        var select = MakeButton(T("Choose file & install", "Dosya seç ve yükle"), () =>
        {
            using var picker = new OpenFileDialog { Filter = fileFilter, Multiselect = true };
            if (picker.ShowDialog(this) == DialogResult.OK) import(picker.FileNames);
        }, icon: "download");
        select.Tag = "primary";
        select.Height = 32;
        select.Dock = DockStyle.Bottom;
        foreach (Control target in new Control[] { card, heading, subtitle, select })
        {
            target.AllowDrop = true;
            target.DragEnter += (_, e) =>
            {
                bool accepted = e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Any(acceptsFile);
                e.Effect = accepted ? DragDropEffects.Copy : DragDropEffects.None;
                if (accepted) card.BackColor = MixColors(SurfaceAlt, Accent, .22);
            };
            target.DragLeave += (_, _) => card.BackColor = SurfaceAlt;
            target.DragDrop += (_, e) =>
            {
                card.BackColor = SurfaceAlt;
                if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Any(acceptsFile)) import(files);
            };
        }
        card.Controls.AddRange([select, subtitle, heading]);
        return card;
    }
}
