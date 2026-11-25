namespace LunaPosLabelStickerMaker;

public class TrialExpiredForm : Form
{
    public TrialExpiredForm()
    {
        Text = "Trial Expired - LunaPOS Label Sticker Maker";
        Size = new Size(540, 420);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.White;
        Font = new Font("Segoe UI", 9f);
        TopMost = true;

        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 110,
            BackColor = Color.FromArgb(254, 242, 242),
            Padding = new Padding(24, 16, 24, 16)
        };

        var iconLbl = new Label
        {
            Text = "🔒",
            Font = new Font("Segoe UI", 28f),
            ForeColor = ModernColors.Danger,
            Location = new Point(20, 20),
            Size = new Size(60, 60),
            TextAlign = ContentAlignment.MiddleCenter
        };

        var titleLbl = new Label
        {
            Text = "30-Minute Trial Period Expired",
            Font = new Font("Segoe UI", 14f, FontStyle.Bold),
            ForeColor = Color.FromArgb(153, 27, 27),
            Location = new Point(88, 22),
            AutoSize = true
        };

        var subLbl = new Label
        {
            Text = "The trial evaluation on this device has ended. Printing is now disabled.",
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = Color.FromArgb(185, 28, 28),
            Location = new Point(90, 52),
            AutoSize = true
        };

        header.Controls.AddRange([iconLbl, titleLbl, subLbl]);

        var bodyPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(28, 20, 28, 16)
        };

        var msgLbl = new Label
        {
            Text = "Thank you for testing LunaPOS Label Sticker Maker!\n\n" +
                   "• This device's trial period has concluded.\n" +
                   "• To continue using the software for production printing without watermarks and without time limits, please activate a full license.\n\n" +
                   $"📞 Contact Phone: {AppConfig.TrialContactPhone}\n" +
                   $"✉️ Contact Email: {AppConfig.TrialContactEmail}",
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = ModernColors.TextDark,
            Dock = DockStyle.Top,
            Height = 160
        };

        var btnPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 50,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 8, 0, 0)
        };

        var btnExit = new ModernButton
        {
            Text = "Close Application",
            DefaultBg = ModernColors.Danger,
            HoverBg = Color.FromArgb(185, 28, 28),
            Width = 140,
            Height = 36,
            Margin = new Padding(8, 0, 0, 0)
        };
        btnExit.Click += (s, e) =>
        {
            DialogResult = DialogResult.OK;
            Close();
            Application.Exit();
        };

        var btnCopy = new ModernButton
        {
            Text = "📋 Copy Contact Info",
            DefaultBg = Color.FromArgb(241, 245, 249),
            HoverBg = Color.FromArgb(226, 232, 240),
            ForeColor = ModernColors.TextDark,
            Width = 160,
            Height = 36,
            Margin = new Padding(0)
        };
        btnCopy.Click += (s, e) =>
        {
            Clipboard.SetText($"LunaPOS Label Sticker Maker\nPhone: {AppConfig.TrialContactPhone}\nEmail: {AppConfig.TrialContactEmail}");
            btnCopy.Text = "✓ Copied!";
        };

        btnPanel.Controls.AddRange([btnExit, btnCopy]);

        bodyPanel.Controls.AddRange([msgLbl, btnPanel]);

        Controls.Add(bodyPanel);
        Controls.Add(header);

        FormClosing += (s, e) =>
        {
            Application.Exit();
        };
    }
}
