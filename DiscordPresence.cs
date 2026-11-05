using DiscordRPC;

namespace KoklukBedrockModloader;

public sealed partial class MainForm
{
    const string DefaultDiscordApplicationId = "1567637573680238662";
    readonly CancellationTokenSource presenceLifetime = new();
    DiscordOptions presenceOptions = new();

    void StartDiscordPresence()
    {
        RefreshDiscordOptions();
        _ = RunDiscordPresence(presenceLifetime.Token);
    }

    void RefreshDiscordOptions()
    {
        var options = settings.Discord ?? new DiscordOptions();
        Volatile.Write(ref presenceOptions, options with
        {
            ContentPath = settings.ContentPath,
            English = settings.Language == "EN",
            ApplicationId = string.IsNullOrWhiteSpace(options.ApplicationId) ? DefaultDiscordApplicationId : options.ApplicationId
        });
    }

    Task RunDiscordPresence(CancellationToken cancel) => Task.Run(async () =>
    {
        DiscordRpcClient? client = null;
        string? applicationId = null;
        PresenceSnapshot? published = null;
        int readyGeneration = 0, sentGeneration = 0;
        try
        {
            while (!cancel.IsCancellationRequested)
            {
                try
                {
                    var options = Volatile.Read(ref presenceOptions);
                    var game = options.Enabled && ValidDiscordId(options.ApplicationId)
                        ? MinecraftInstallation.FindRunningGame(options.ContentPath) : null;
                    if (game is null || applicationId != options.ApplicationId)
                    {
                        client?.Dispose();
                        client = null;
                        published = null;
                        applicationId = options.ApplicationId;
                    }
                    if (game is not null)
                    {
                        if (client == null)
                        {
                            Volatile.Write(ref readyGeneration, 0);
                            sentGeneration = 0;
                            client = new DiscordRpcClient(options.ApplicationId);
                            client.OnError += (_, _) => LogAction("Discord rejected a presence request.");
                            client.OnReady += (_, _) => Interlocked.Increment(ref readyGeneration);
                            client.OnPresenceUpdate += (_, _) => LogAction("Discord acknowledged a presence update.");
                        }
                        if (!client.IsInitialized) client.Initialize();

                        var snapshot = new PresenceSnapshot(
                            options.ShowVersion && game.Version is { } version
                                ? (options.English ? "Version: " : "Sürüm: ") + version : null,
                            options.ShowTime ? game.StartedUtc : null,
                            string.IsNullOrWhiteSpace(options.LargeImage) ? "bedrockloader" : options.LargeImage, options.SmallImage);
                        int generation = Volatile.Read(ref readyGeneration);
                        if (client.IsInitialized && generation > 0 && (snapshot != published || generation != sentGeneration))
                        {
                            client.SetPresence(new RichPresence
                            {
                                Details = snapshot.Details,
                                Timestamps = snapshot.StartedUtc is { } started ? new Timestamps { Start = started } : null,
                                Assets = new Assets { LargeImageKey = snapshot.LargeImage, SmallImageKey = snapshot.SmallImage }
                            });
                            published = snapshot;
                            sentGeneration = generation;
                        }
                    }
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    client?.Dispose();
                    client = null;
                    published = null;
                }
                await Task.Delay(TimeSpan.FromSeconds(3), cancel);
            }
        }
        catch (OperationCanceledException) when (cancel.IsCancellationRequested) { }
        finally { client?.Dispose(); }
    });

    static bool ValidDiscordId(string value) => ulong.TryParse(value, System.Globalization.NumberStyles.None,
        System.Globalization.CultureInfo.InvariantCulture, out ulong id) && id > 0;

    sealed record PresenceSnapshot(string? Details, DateTime? StartedUtc, string LargeImage, string SmallImage);

    void ConfigureDiscordPresence()
    {
        var options = settings.Discord ?? new DiscordOptions();
        using var dialog = new RoundedPopupForm(Accent)
        {
            Text = "Discord Rich Presence", StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.None,
            ClientSize = new Size(570, 365), AutoScaleMode = AutoScaleMode.Dpi, BackColor = Surface, ForeColor = TextColor,
            ShowInTaskbar = false, MinimizeBox = false, MaximizeBox = false, Font = Font
        };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 9, Padding = new Padding(20), AutoScroll = true };
        int row = 0;
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54));
        dialog.Controls.Add(layout);
        Span(new Label { Text = "Discord Rich Presence", Font = new Font(Font.FontFamily, 16, FontStyle.Bold), Dock = DockStyle.Fill }, 38);
        var enabled = Check(T("Enable Rich Presence", "Rich Presence’ı etkinleştir"), options.Enabled);
        var version = Check(T("Share Minecraft version", "Minecraft sürümünü paylaş"), options.ShowVersion);
        var time = Check(T("Share elapsed time", "Oyun süresini paylaş"), options.ShowTime);
        var id = Field(T("Discord Application ID", "Discord uygulama kimliği"), string.IsNullOrWhiteSpace(options.ApplicationId) ? DefaultDiscordApplicationId : options.ApplicationId, 20);
        var large = Field(T("Large image key", "Büyük görsel anahtarı"), options.LargeImage, 64);
        var small = Field(T("Small image key", "Küçük görsel anahtarı"), options.SmallImage, 64);
        var hint = new Label { ForeColor = lightMode ? Color.DarkRed : Color.LightSalmon };
        Span(hint, 25);
        id.TextChanged += (_, _) => hint.Text = "";
        var cancel = MakeButton(T("Cancel", "Vazgeç"), () => dialog.DialogResult = DialogResult.Cancel);
        var save = MakeButton(T("Save", "Kaydet"), () =>
        {
            string appId = id.Text.Trim();
            if (appId.Length > 0 && !ValidDiscordId(appId))
            {
                hint.Text = T("Enter a numeric Discord Application ID.", "Sayısal bir Discord uygulama kimliği gir.");
                return;
            }
            settings.Discord = new DiscordOptions
            {
                Enabled = enabled.Checked, ShowVersion = version.Checked, ShowTime = time.Checked,
                ApplicationId = appId, LargeImage = large.Text.Trim(), SmallImage = small.Text.Trim()
            };
            SaveSettings();
            RefreshDiscordOptions();
            dialog.DialogResult = DialogResult.OK;
        });
        save.Tag = "primary";
        ApplyButtonPalette(save);
        ApplyButtonPalette(cancel);
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        cancel.Anchor = save.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        cancel.Margin = new Padding(0, 0, 5, 0);
        save.Margin = new Padding(5, 0, 0, 0);
        layout.Controls.Add(cancel, 0, row); layout.Controls.Add(save, 1, row);
        dialog.CancelButton = cancel;
        dialog.AcceptButton = save;
        dialog.ShowDialog(this);

        CheckBox Check(string label, bool value)
        {
            var box = new PopupToggle(Accent, SurfaceAlt) { Text = label, Checked = value };
            Span(box, 25);
            return box;
        }
        void AddRow(string label, Control control)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            layout.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
            control.Dock = DockStyle.Fill;
            control.Margin = new Padding(2, 1, 2, 2);
            layout.Controls.Add(control, 1, row++);
        }
        void Span(Control control, int height)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            control.Dock = DockStyle.Fill;
            layout.Controls.Add(control, 0, row++);
            layout.SetColumnSpan(control, 2);
        }
        TextBox Field(string label, string value, int length)
        {
            var input = MakeTextBox();
            input.Text = value;
            input.MaxLength = length;
            input.AccessibleName = label;
            AddRow(label, WrapPopupInput(input));
            return input;
        }
    }

    sealed record DiscordOptions
    {
        public bool Enabled { get; init; } = true;
        public bool ShowVersion { get; init; } = true;
        public bool ShowTime { get; init; } = true;
        public string ApplicationId { get; init; } = DefaultDiscordApplicationId;
        public string LargeImage { get; init; } = "bedrockloader";
        public string SmallImage { get; init; } = "";
        [System.Text.Json.Serialization.JsonIgnore]
        public string ContentPath { get; init; } = "";
        [System.Text.Json.Serialization.JsonIgnore]
        public bool English { get; init; }
    }
}
