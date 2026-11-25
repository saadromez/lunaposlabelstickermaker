namespace LunaPosLabelStickerMaker;

public class UpdateProgressDialog : Form
{
    private readonly UpdateCheckResult _updateInfo;
    private readonly CancellationTokenSource _cts = new();
    private ProgressBar _progressBar = null!;
    private Label _lblStatus = null!;
    private Label _lblBytes = null!;
    private ModernButton _btnCancel = null!;
    private bool _downloadCompleted = false;

    public UpdateProgressDialog(UpdateCheckResult updateInfo)
    {
        _updateInfo = updateInfo;

        Text = "LunaPOS Software Update";
        Size = new Size(480, 290);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.White;
        Font = new Font("Segoe UI", 9f);

        BuildUi();
    }

    private void BuildUi()
    {
        // Header
        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 65,
            BackColor = Color.FromArgb(248, 250, 253),
            Padding = new Padding(16, 12, 16, 12)
        };

        var icon = new Label
        {
            Text = "🚀",
            Font = new Font("Segoe UI", 20f),
            Location = new Point(14, 12),
            Size = new Size(36, 36),
            TextAlign = ContentAlignment.MiddleCenter
        };

        var title = new Label
        {
            Text = $"Updating LunaPOS Label Studio to v{_updateInfo.LatestVersion}",
            Font = new Font("Segoe UI", 11.5f, FontStyle.Bold),
            ForeColor = ModernColors.TextDark,
            Location = new Point(56, 12),
            AutoSize = true
        };

        var sub = new Label
        {
            Text = $"Current version: v{_updateInfo.CurrentVersion} • Official LunaPOS Release",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = ModernColors.TextMuted,
            Location = new Point(58, 36),
            AutoSize = true
        };

        header.Controls.AddRange([icon, title, sub]);

        // Body
        var body = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24, 16, 24, 16)
        };

        _lblStatus = new Label
        {
            Text = "Connecting to LunaPOS update server...",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = ModernColors.TextDark,
            Location = new Point(24, 12),
            AutoSize = true
        };

        _progressBar = new ProgressBar
        {
            Location = new Point(24, 40),
            Width = 416,
            Height = 22,
            Style = ProgressBarStyle.Continuous,
            Minimum = 0,
            Maximum = 100,
            Value = 0
        };

        _lblBytes = new Label
        {
            Text = "Preparing download...",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = ModernColors.TextMuted,
            Location = new Point(24, 68),
            AutoSize = true
        };

        if (!string.IsNullOrWhiteSpace(_updateInfo.Changelog))
        {
            var lblNotes = new Label
            {
                Text = $"What's new: {_updateInfo.Changelog.Replace("\n", " • ")}",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = ModernColors.TextDark,
                Location = new Point(24, 94),
                Width = 416,
                Height = 36
            };
            body.Controls.Add(lblNotes);
        }

        // Bottom Actions
        var bottomPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 50,
            BackColor = Color.FromArgb(250, 251, 254),
            Padding = new Padding(16, 8, 20, 8)
        };

        _btnCancel = new ModernButton
        {
            Text = "Cancel",
            DefaultBg = Color.FromArgb(241, 245, 249),
            HoverBg = Color.FromArgb(226, 232, 240),
            ForeColor = ModernColors.TextDark,
            Width = 95,
            Height = 32,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(bottomPanel.Width - 115, 9)
        };
        _btnCancel.Click += (s, e) =>
        {
            _cts.Cancel();
            Close();
        };

        bottomPanel.Controls.Add(_btnCancel);

        body.Controls.AddRange([_lblStatus, _progressBar, _lblBytes]);

        Controls.Add(body);
        Controls.Add(bottomPanel);
        Controls.Add(header);

        Shown += async (s, e) => await StartDownloadAsync();
        FormClosing += (s, e) =>
        {
            if (!_downloadCompleted)
            {
                _cts.Cancel();
            }
        };
    }

    private async Task StartDownloadAsync()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "LunaPOS_Updates");
        Directory.CreateDirectory(tempDir);
        string targetFile = Path.Combine(tempDir, $"LunaPOS_Setup_v{_updateInfo.LatestVersion}.exe");

        var progress = new Progress<(long received, long total, int percentage)>(p =>
        {
            if (IsDisposed) return;

            _progressBar.Value = Math.Clamp(p.percentage, 0, 100);
            if (p.total > 0)
            {
                double mbReceived = p.received / (1024.0 * 1024.0);
                double mbTotal = p.total / (1024.0 * 1024.0);
                _lblBytes.Text = $"{mbReceived:F1} MB of {mbTotal:F1} MB ({p.percentage}%)";
            }
            else
            {
                double mbReceived = p.received / (1024.0 * 1024.0);
                _lblBytes.Text = $"{mbReceived:F1} MB downloaded";
            }
            _lblStatus.Text = "Downloading update package...";
        });

        try
        {
            await UpdateManager.DownloadUpdateAsync(_updateInfo.DownloadUrl, targetFile, progress, _cts.Token);

            _downloadCompleted = true;
            _lblStatus.Text = "✓ Download complete! Launching installer...";
            _lblStatus.ForeColor = ModernColors.Success;
            _lblBytes.Text = "Application will close to apply update.";
            _btnCancel.Enabled = false;

            await Task.Delay(1000);

            UpdateManager.ApplyUpdateAndRestart(targetFile);
        }
        catch (OperationCanceledException)
        {
            // User cancelled
        }
        catch (Exception)
        {
            if (!IsDisposed)
            {
                _lblStatus.Text = "✗ Update download failed.";
                _lblStatus.ForeColor = ModernColors.Danger;
                _lblBytes.Text = "Please verify your internet connection and try again.";
                _btnCancel.Text = "Close";
            }
        }
    }
}
