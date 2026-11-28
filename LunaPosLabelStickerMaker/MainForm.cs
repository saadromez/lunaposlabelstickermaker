using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.Text.Json;

namespace LunaPosLabelStickerMaker;

public class MainForm : Form
{
    private readonly DataStore _store = new();
    private ProductItem? _selectedProduct;
    private LabelTemplate _activeTemplate;
    private readonly PrintJob _currentJob = new();

    // Navigation
    private readonly Panel _sidebar;
    private readonly List<SidebarTabButton> _navButtons = [];
    private readonly Panel _contentContainer;

    // View Panels
    private Panel _printStudioView = null!;
    private Panel _productsView = null!;
    private Panel _templatesView = null!;
    private Panel _historyView = null!;
    private Panel _settingsView = null!;

    // Print Studio Controls
    private TextBox _studioSearchBox = null!;
    private ListView _studioProductList = null!;
    private TextBox _txtStudioName = null!;
    private TextBox _txtStudioSinhalaName = null!;
    private DateTimePicker _dtpMDate = null!;
    private DateTimePicker _dtpExpDate = null!;
    private TextBox _txtStudioBatch = null!;
    private TextBox _txtStudioPrice = null!;
    private TextBox _txtStudioWeight = null!;
    private TextBox _txtStudioDesc = null!;
    private TextBox _txtStudioPhone = null!;
    private TextBox _txtStudioReg = null!;
    private ComboBox _cboStudioTemplate = null!;
    private NumericUpDown _numStudioWidth = null!;
    private NumericUpDown _numStudioHeight = null!;
    private NumericUpDown _numCopies = null!;
    private PictureBox _previewBox = null!;
    private Label _lblPreviewSize = null!;
    private Label _lblStatus = null!;
    private ModernButton _btnPrint = null!;
    private Label _lblHeaderPrinter = null!;

    // Phrase Customizer Controls
    private ComboBox _cboPhraseSelector = null!;
    private NumericUpDown _numPhraseFontSize = null!;
    private NumericUpDown _numPhraseOffsetX = null!;
    private NumericUpDown _numPhraseOffsetY = null!;
    private ComboBox _cboPhraseFont = null!;
    private CheckBox _chkPhraseVisible = null!;
    private CheckBox _chkPhraseBold = null!;
    private ComboBox _cboPhraseRotation = null!;
    private ModernButton _btnRotatePhrase = null!;
    private Label _lblPhraseOffsetStatus = null!;
    private bool _isUpdatingPhraseControls = false;

    // Products View Controls
    private ListView _productsTable = null!;
    private TextBox _productSearchBox = null!;

    // Templates View Controls
    private ListView _templateList = null!;
    private TextBox _txtTplName = null!;
    private NumericUpDown _numTplWidth = null!;
    private NumericUpDown _numTplHeight = null!;
    private NumericUpDown _numTplDpi = null!;
    private ComboBox _cboTplFont = null!;
    private ComboBox _cboTplSinhalaFont = null!;
    private NumericUpDown _numTitleFont = null!;
    private NumericUpDown _numBodyFont = null!;
    private CheckBox _chkSinhala = null!;
    private CheckBox _chkMDate = null!;
    private CheckBox _chkExpDate = null!;
    private CheckBox _chkBatch = null!;
    private CheckBox _chkPrice = null!;
    private CheckBox _chkWeight = null!;
    private CheckBox _chkDesc = null!;
    private CheckBox _chkPhone = null!;
    private CheckBox _chkReg = null!;
    private CheckBox _chkBarcode = null!;
    private PictureBox _templatePreviewBox = null!;

    // Settings View Controls
    private RadioButton _rbConnWindows = null!;
    private RadioButton _rbConnBluetooth = null!;
    private Panel _pnlWindowsPrinter = null!;
    private Panel _pnlBluetoothPrinter = null!;
    private ComboBox _cboSettingsPrinter = null!;
    private ComboBox _cboSettingsEngine = null!;
    private ComboBox _cboBluetoothPort = null!;
    private ComboBox _cboBluetoothBaud = null!;
    private ComboBox _cboBluetoothProtocol = null!;
    private TextBox _txtSettingsCompany = null!;
    private TextBox _txtSettingsPhone = null!;
    private TextBox _txtSettingsCurrency = null!;

    // History View Controls
    private ListView _historyTable = null!;

    // Trial Controls
    private System.Windows.Forms.Timer? _trialUiTimer;
    private Label? _lblHeaderTrialBadge;
    private Label? _lblTrialBanner;

    // Update Controls
    private Panel? _pnlUpdateBanner;
    private CheckBox _chkAutoUpdate = null!;
    private Label _lblUpdateStatus = null!;
    private ModernButton _btnCheckUpdates = null!;

    public MainForm()
    {
        Text = AppConfig.IsTrial ? "LunaPOS Label Sticker Maker [30-MINUTE TRIAL VERSION]" : "LunaPOS Label Sticker Maker";
        Size = new Size(1280, 800);
        MinimumSize = new Size(1100, 700);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = ModernColors.AppBackground;
        Font = new Font("Segoe UI", 9f);
        KeyPreview = true;

        _activeTemplate = _store.Templates.FirstOrDefault() ?? DataStore.GetDefaultTemplates()[0];
        _selectedProduct = _store.Products.FirstOrDefault();

        // 1. Sidebar Navigation (Left)
        _sidebar = CreateSidebar();

        // 2. Main Right Container (Header + Content)
        var mainRightPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = ModernColors.AppBackground
        };

        // Header Bar
        var headerBar = CreateHeaderBar();

        // Content Container
        _contentContainer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = ModernColors.AppBackground,
            Padding = new Padding(16, 12, 16, 16)
        };

        mainRightPanel.Controls.Add(_contentContainer);
        mainRightPanel.Controls.Add(headerBar);

        Controls.Add(mainRightPanel);
        Controls.Add(_sidebar);

        // Build View Panels
        BuildPrintStudioView();
        BuildProductsView();
        BuildTemplatesView();
        BuildHistoryView();
        BuildSettingsView();

        // Initialize with default product and template
        SwitchTab("studio");
        LoadPrinters();

        if (_selectedProduct != null)
        {
            SelectProductForPrinting(_selectedProduct);
        }

        if (AppConfig.IsTrial)
        {
            TrialManager.OnTrialExpired += OnTrialExpiredHandler;
            _trialUiTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _trialUiTimer.Tick += (s, e) => UpdateTrialUiCountdown();
            _trialUiTimer.Start();
            UpdateTrialUiCountdown();
        }
        else if (_store.Settings.AutoCheckUpdates)
        {
            // Production edition: automatically check for LunaPOS cloud updates silently on startup
            Task.Run(async () =>
            {
                await Task.Delay(2500);
                var check = await UpdateManager.CheckForUpdatesAsync();
                if (check.HasUpdate && !IsDisposed)
                {
                    Invoke(() => ShowUpdateBanner(check));
                }
            });
        }

        KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.F5)
            {
                e.Handled = true;
                ExecutePrint();
            }
        };
    }

    private Panel CreateSidebar()
    {
        var sb = new Panel
        {
            Dock = DockStyle.Left,
            Width = 230,
            BackColor = ModernColors.SidebarBg
        };

        // Logo / Title header
        var logoPanel = new Panel { Dock = DockStyle.Top, Height = 74, BackColor = Color.Transparent };
        var logoIcon = new Label
        {
            Text = "🏷️",
            Font = new Font("Segoe UI", 16f),
            ForeColor = Color.White,
            Location = new Point(12, 16),
            Size = new Size(32, 32),
            TextAlign = ContentAlignment.MiddleCenter
        };
        var appTitle = new Label
        {
            Text = "LunaPOS",
            Font = new Font("Segoe UI", 13.5f, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(48, 14),
            AutoSize = true
        };
        var appSub = new Label
        {
            Text = "Label Sticker Maker",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = ModernColors.TextLight,
            Location = new Point(50, 38),
            AutoSize = true
        };
        logoPanel.Controls.AddRange([logoIcon, appTitle, appSub]);
        sb.Controls.Add(logoPanel);

        // Navigation Items
        int y = 84;
        AddNavButton(sb, "studio", "Print Studio", "🖨️", ref y);
        AddNavButton(sb, "products", "Products Catalog", "📦", ref y);
        AddNavButton(sb, "templates", "Label Templates", "📐", ref y);
        AddNavButton(sb, "history", "Print History", "📜", ref y);
        AddNavButton(sb, "settings", "Settings", "⚙️", ref y);

        // Sidebar Footer
        var footer = new Panel { Dock = DockStyle.Bottom, Height = 48, BackColor = Color.Transparent };
        var verLbl = new Label
        {
            Text = "LunaPOS Studio v1.0\nReady to Print",
            Font = new Font("Segoe UI", 7.5f),
            ForeColor = Color.FromArgb(120, 130, 160),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter
        };
        footer.Controls.Add(verLbl);
        sb.Controls.Add(footer);

        return sb;
    }

    private void AddNavButton(Panel parent, string tabId, string title, string icon, ref int y)
    {
        var btn = new SidebarTabButton
        {
            TabId = tabId,
            Text = title,
            IconText = icon,
            Location = new Point(0, y),
            Width = 230,
            Height = 44
        };
        btn.Click += (s, e) => SwitchTab(tabId);
        _navButtons.Add(btn);
        parent.Controls.Add(btn);
        y += 48;
    }

    private Panel CreateHeaderBar()
    {
        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 56,
            BackColor = Color.White,
            Padding = new Padding(16, 8, 16, 8)
        };

        // Bottom border line
        header.Paint += (s, e) =>
        {
            using var pen = new Pen(ModernColors.Border, 1f);
            e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
        };

        // Header Title / Breadcrumb
        var lblTitle = new Label
        {
            Text = "Product & Label Sticker Studio",
            Font = new Font("Segoe UI", 11.5f, FontStyle.Bold),
            ForeColor = ModernColors.TextDark,
            Location = new Point(16, 14),
            AutoSize = true
        };

        // Header Right Panel (Printer Badge + Print Button)
        var headerRight = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 3, 0, 0)
        };

        // Active Printer Badge
        _lblHeaderPrinter = new Label
        {
            Text = "🖨️ Printer: Loading...",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = ModernColors.Primary,
            BackColor = ModernColors.PrimaryLight,
            Padding = new Padding(10, 6, 10, 6),
            AutoSize = true,
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 2, 10, 0)
        };
        _lblHeaderPrinter.Click += (s, e) => SwitchTab("settings");

        // Header Print Shortcut Button
        var btnQuickPrint = new ModernButton
        {
            Text = "🖨️ Print Label (F5)",
            DefaultBg = ModernColors.Primary,
            HoverBg = ModernColors.PrimaryHover,
            Width = 145,
            Height = 34,
            Margin = new Padding(0)
        };
        btnQuickPrint.Click += (s, e) => ExecutePrint();

        if (AppConfig.IsTrial)
        {
            _lblHeaderTrialBadge = new Label
            {
                Text = "⚠️ TRIAL (30m)",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = ModernColors.Danger,
                BackColor = Color.FromArgb(254, 242, 242),
                Padding = new Padding(8, 6, 8, 6),
                AutoSize = true,
                Margin = new Padding(0, 2, 8, 0)
            };
            headerRight.Controls.Add(_lblHeaderTrialBadge);
        }

        headerRight.Controls.AddRange([_lblHeaderPrinter, btnQuickPrint]);

        header.Controls.Add(headerRight);
        header.Controls.Add(lblTitle);
        return header;
    }

    private void SwitchTab(string tabId)
    {
        foreach (var b in _navButtons)
        {
            b.IsActive = (b.TabId == tabId);
            b.Invalidate();
        }

        _contentContainer.Controls.Clear();

        switch (tabId)
        {
            case "studio":
                _contentContainer.Controls.Add(_printStudioView);
                RefreshStudioPreview();
                break;
            case "products":
                RefreshProductsTable();
                _contentContainer.Controls.Add(_productsView);
                break;
            case "templates":
                RefreshTemplatesList();
                _contentContainer.Controls.Add(_templatesView);
                break;
            case "history":
                RefreshHistoryTable();
                _contentContainer.Controls.Add(_historyView);
                break;
            case "settings":
                _contentContainer.Controls.Add(_settingsView);
                break;
        }
    }

    #region Print Studio View

    private void BuildPrintStudioView()
    {
        _printStudioView = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };

        Panel? trialBanner = null;
        if (AppConfig.IsTrial)
        {
            trialBanner = new Panel
            {
                Dock = DockStyle.Top,
                Height = 34,
                BackColor = Color.FromArgb(254, 242, 242),
                Padding = new Padding(12, 6, 12, 6)
            };
            trialBanner.Paint += (s, e) =>
            {
                using var p = new Pen(Color.FromArgb(252, 165, 165), 1.2f);
                e.Graphics.DrawRectangle(p, 0, 0, trialBanner.Width - 1, trialBanner.Height - 1);
            };

            _lblTrialBanner = new Label
            {
                Text = "⚠️ 30-MINUTE TRIAL EVALUATION: Calculating remaining time... Permanent watermarks are applied to all stickers.",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(185, 28, 28),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            trialBanner.Controls.Add(_lblTrialBanner);
        }

        // 1. Welcome Card Banner (matching Reference Image 2 style)
        var banner = new ModernCard
        {
            Dock = DockStyle.Top,
            Height = 78,
            BackColor = Color.White,
            CornerRadius = 14,
            ShowBorder = true,
            Padding = new Padding(16, 10, 16, 10)
        };

        var bannerTitle = new Label
        {
            Text = "Welcome to LunaPOS Label Studio",
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = ModernColors.TextDark,
            Location = new Point(14, 12),
            AutoSize = true
        };
        var bannerSub = new Label
        {
            Text = "Select a product, adjust dates/prices on the fly, and print high-quality stickers with Sinhala & English typography.",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = ModernColors.TextMuted,
            Location = new Point(15, 36),
            AutoSize = true
        };

        // Quick Stats Pills on Banner
        var bannerRight = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 14, 10, 0)
        };
        var pillCount = new PillChip
        {
            Text = $"📦 {_store.Products.Count} Products",
            Selected = true,
            ActiveBg = Color.FromArgb(238, 242, 255),
            ActiveText = ModernColors.Primary,
            Size = new Size(110, 28),
            Margin = new Padding(0, 0, 8, 0)
        };
        var pillReady = new PillChip
        {
            Text = "✓ Ready",
            Selected = true,
            ActiveBg = Color.FromArgb(236, 253, 245),
            ActiveText = ModernColors.Success,
            Size = new Size(80, 28),
            Margin = new Padding(0)
        };
        bannerRight.Controls.AddRange([pillCount, pillReady]);
        banner.Controls.AddRange([bannerTitle, bannerSub, bannerRight]);

        // Main Work Area Panel (3 Columns)
        var workPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 12, 0, 0) };

        // Column 1: Products Selector (Left, 235px)
        var colProducts = new ModernCard
        {
            Dock = DockStyle.Left,
            Width = 235,
            CornerRadius = 14,
            Padding = new Padding(10)
        };

        var lblCol1 = new Label
        {
            Text = "📦 Products",
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            ForeColor = ModernColors.TextDark,
            Location = new Point(10, 10),
            AutoSize = true
        };

        _studioSearchBox = new TextBox
        {
            Location = new Point(10, 38),
            Width = 215,
            Font = new Font("Segoe UI", 9.5f),
            PlaceholderText = "🔍 Search products..."
        };
        _studioSearchBox.TextChanged += (s, e) => FilterStudioProducts();

        _studioProductList = new ListView
        {
            Location = new Point(10, 72),
            Width = 215,
            Height = 520,
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            View = View.Details,
            FullRowSelect = true,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
            BorderStyle = BorderStyle.None,
            BackColor = Color.FromArgb(250, 251, 254)
        };
        _studioProductList.Columns.Add("Product", 130);
        _studioProductList.Columns.Add("Price", 75);
        _studioProductList.SelectedIndexChanged += OnStudioProductSelected;

        var btnAddQuick = new ModernButton
        {
            Text = "+ Add New Product",
            DefaultBg = Color.FromArgb(241, 245, 249),
            HoverBg = Color.FromArgb(226, 232, 240),
            ForeColor = ModernColors.TextDark,
            Width = 215,
            Height = 34,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            Location = new Point(10, colProducts.Height - 48)
        };
        btnAddQuick.Click += (s, e) => OpenProductEditDialog(null);

        colProducts.Controls.AddRange([lblCol1, _studioSearchBox, _studioProductList, btnAddQuick]);

        // Column 2: Live Print Settings & Field Editor (Center, 330px)
        var colSettings = new ModernCard
        {
            Dock = DockStyle.Left,
            Width = 330,
            CornerRadius = 14,
            Padding = new Padding(12),
            AutoScroll = true
        };
        colSettings.Location = new Point(247, 0);

        var lblCol2 = new Label
        {
            Text = "⚡ Customize & Print",
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            ForeColor = ModernColors.TextDark,
            Location = new Point(12, 10),
            AutoSize = true
        };

        int sy = 40;

        // Product English & Sinhala Title
        AddFormLabel(colSettings, "Product Title (English)", 12, ref sy);
        _txtStudioName = AddFormTextBox(colSettings, 12, ref sy, 300);
        _txtStudioName.TextChanged += (s, e) => RefreshStudioPreview();

        AddFormLabel(colSettings, "Sinhala Title (සිංහල නම)", 12, ref sy);
        _txtStudioSinhalaName = AddFormTextBox(colSettings, 12, ref sy, 300);
        _txtStudioSinhalaName.Font = new Font("Nirmala UI", 9.5f, FontStyle.Bold);
        _txtStudioSinhalaName.TextChanged += (s, e) => RefreshStudioPreview();

        // Manufacture Date
        AddFormLabel(colSettings, "M:DATE (නිෂ්පාදිත දිනය)", 12, ref sy);
        _dtpMDate = new DateTimePicker
        {
            Location = new Point(12, sy),
            Width = 160,
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "yyyy/MM/dd",
            Font = new Font("Segoe UI", 9.5f)
        };
        _dtpMDate.ValueChanged += (s, e) =>
        {
            if (_store.Settings.AutoCalculateExpiry && _selectedProduct != null)
            {
                _dtpExpDate.Value = _dtpMDate.Value.AddDays(_selectedProduct.ShelfLifeDays > 0 ? _selectedProduct.ShelfLifeDays : 30);
            }
            RefreshStudioPreview();
        };
        colSettings.Controls.Add(_dtpMDate);

        var chipToday = new PillChip { Text = "Today", Location = new Point(180, sy), Size = new Size(58, 26) };
        chipToday.Click += (s, e) => { _dtpMDate.Value = DateTime.Today; };
        var chipYest = new PillChip { Text = "Yest.", Location = new Point(244, sy), Size = new Size(58, 26) };
        chipYest.Click += (s, e) => { _dtpMDate.Value = DateTime.Today.AddDays(-1); };
        colSettings.Controls.AddRange([chipToday, chipYest]);
        sy += 34;

        // Expiry Date
        AddFormLabel(colSettings, "EXP : DATE (කල්ඉකුත්වීමේ දිනය)", 12, ref sy);
        _dtpExpDate = new DateTimePicker
        {
            Location = new Point(12, sy),
            Width = 160,
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "yyyy/MM/dd",
            Font = new Font("Segoe UI", 9.5f)
        };
        _dtpExpDate.ValueChanged += (s, e) => RefreshStudioPreview();
        colSettings.Controls.Add(_dtpExpDate);

        var chipPlus14 = new PillChip { Text = "+14d", Location = new Point(180, sy), Size = new Size(38, 26) };
        chipPlus14.Click += (s, e) => { _dtpExpDate.Value = _dtpMDate.Value.AddDays(14); };
        var chipPlus30 = new PillChip { Text = "+30d", Location = new Point(222, sy), Size = new Size(38, 26) };
        chipPlus30.Click += (s, e) => { _dtpExpDate.Value = _dtpMDate.Value.AddDays(30); };
        var chipPlus90 = new PillChip { Text = "+90d", Location = new Point(264, sy), Size = new Size(38, 26) };
        chipPlus90.Click += (s, e) => { _dtpExpDate.Value = _dtpMDate.Value.AddDays(90); };
        colSettings.Controls.AddRange([chipPlus14, chipPlus30, chipPlus90]);
        sy += 34;

        // Batch Number & Price Row
        var lblBatch = new Label { Text = "BATCH NO", Location = new Point(12, sy), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
        var lblPrice = new Label { Text = "PRICE (මිල)", Location = new Point(162, sy), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
        colSettings.Controls.AddRange([lblBatch, lblPrice]);
        sy += 20;

        _txtStudioBatch = new TextBox { Location = new Point(12, sy), Width = 142, Font = new Font("Segoe UI", 9.5f) };
        _txtStudioBatch.TextChanged += (s, e) => RefreshStudioPreview();
        _txtStudioPrice = new TextBox { Location = new Point(162, sy), Width = 150, Font = new Font("Segoe UI", 9.5f) };
        _txtStudioPrice.TextChanged += (s, e) => RefreshStudioPreview();
        colSettings.Controls.AddRange([_txtStudioBatch, _txtStudioPrice]);
        sy += 32;

        // Weight & Phone Row
        var lblWeight = new Label { Text = "WEIGHT (බර)", Location = new Point(12, sy), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
        var lblPhone = new Label { Text = "CONTACT / TEL", Location = new Point(162, sy), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
        colSettings.Controls.AddRange([lblWeight, lblPhone]);
        sy += 20;

        _txtStudioWeight = new TextBox { Location = new Point(12, sy), Width = 142, Font = new Font("Segoe UI", 9.5f) };
        _txtStudioWeight.TextChanged += (s, e) => RefreshStudioPreview();
        _txtStudioPhone = new TextBox { Location = new Point(162, sy), Width = 150, Font = new Font("Segoe UI", 9.5f) };
        _txtStudioPhone.TextChanged += (s, e) => RefreshStudioPreview();
        colSettings.Controls.AddRange([_txtStudioWeight, _txtStudioPhone]);
        sy += 32;

        // Description / Ingredients
        AddFormLabel(colSettings, "Description / Ingredients / Sinhala Notes", 12, ref sy);
        _txtStudioDesc = new TextBox { Location = new Point(12, sy), Width = 300, Height = 42, Multiline = true, Font = new Font("Nirmala UI", 8.5f) };
        _txtStudioDesc.TextChanged += (s, e) => RefreshStudioPreview();
        colSettings.Controls.Add(_txtStudioDesc);
        sy += 48;

        // Registration Side Text
        AddFormLabel(colSettings, "Registration / License No (දි:ව ලියාපදිංචි)", 12, ref sy);
        _txtStudioReg = AddFormTextBox(colSettings, 12, ref sy, 300);
        _txtStudioReg.Font = new Font("Nirmala UI", 8.5f);
        _txtStudioReg.TextChanged += (s, e) => RefreshStudioPreview();

        // Template Selection
        AddFormLabel(colSettings, "📐 Label Template", 12, ref sy);
        _cboStudioTemplate = new ComboBox
        {
            Location = new Point(12, sy),
            Width = 300,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 9.5f)
        };
        _cboStudioTemplate.SelectedIndexChanged += (s, e) =>
        {
            if (_cboStudioTemplate.SelectedItem is LabelTemplate tpl)
            {
                _activeTemplate = tpl;
                if (_numStudioWidth != null && _numStudioHeight != null)
                {
                    _numStudioWidth.Value = (decimal)tpl.WidthMm;
                    _numStudioHeight.Value = (decimal)tpl.HeightMm;
                }
                RefreshStudioPreview();
            }
        };
        colSettings.Controls.Add(_cboStudioTemplate);
        sy += 32;

        // Customizable Size on the Fly
        var lblSize = new Label { Text = "📐 Label Size (mm)", Location = new Point(12, sy), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
        colSettings.Controls.Add(lblSize);
        sy += 20;

        var lblW = new Label { Text = "W:", Location = new Point(12, sy + 3), AutoSize = true, Font = new Font("Segoe UI", 8.5f) };
        _numStudioWidth = new NumericUpDown
        {
            Location = new Point(34, sy),
            Width = 72,
            Minimum = 10,
            Maximum = 300,
            Value = (decimal)_activeTemplate.WidthMm,
            DecimalPlaces = 1,
            Font = new Font("Segoe UI", 9f)
        };
        var lblH = new Label { Text = "H:", Location = new Point(114, sy + 3), AutoSize = true, Font = new Font("Segoe UI", 8.5f) };
        _numStudioHeight = new NumericUpDown
        {
            Location = new Point(134, sy),
            Width = 72,
            Minimum = 10,
            Maximum = 300,
            Value = (decimal)_activeTemplate.HeightMm,
            DecimalPlaces = 1,
            Font = new Font("Segoe UI", 9f)
        };
        _numStudioWidth.ValueChanged += (s, e) =>
        {
            _activeTemplate.WidthMm = (float)_numStudioWidth.Value;
            RefreshStudioPreview();
        };
        _numStudioHeight.ValueChanged += (s, e) =>
        {
            _activeTemplate.HeightMm = (float)_numStudioHeight.Value;
            RefreshStudioPreview();
        };
        colSettings.Controls.AddRange([lblW, _numStudioWidth, lblH, _numStudioHeight]);
        sy += 30;

        // Preset size chips row (compact FlowLayoutPanel)
        var pnlPresets = new FlowLayoutPanel
        {
            Location = new Point(12, sy),
            Width = 300,
            Height = 30,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            Padding = new Padding(0),
            WrapContents = false
        };
        (string name, float w, float h)[] presets = [("40×45", 40, 45), ("50×40", 50, 40), ("50×50", 50, 50), ("40×30", 40, 30), ("60×40", 60, 40)];
        foreach (var p in presets)
        {
            var pr = p;
            var chip = new PillChip { Text = pr.name, Size = new Size(52, 25), Margin = new Padding(0, 0, 4, 0) };
            chip.Click += (s, e) =>
            {
                _numStudioWidth.Value = (decimal)pr.w;
                _numStudioHeight.Value = (decimal)pr.h;
            };
            pnlPresets.Controls.Add(chip);
        }
        colSettings.Controls.Add(pnlPresets);
        sy += 34;

        // Copies Row
        AddFormLabel(colSettings, "Print Copies", 12, ref sy);
        _numCopies = new NumericUpDown
        {
            Location = new Point(12, sy),
            Width = 65,
            Minimum = 1,
            Maximum = 9999,
            Value = 1,
            Font = new Font("Segoe UI", 9.5f)
        };
        colSettings.Controls.Add(_numCopies);

        var pnlCopies = new FlowLayoutPanel
        {
            Location = new Point(84, sy),
            Width = 230,
            Height = 30,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            Padding = new Padding(0),
            WrapContents = false
        };
        int[] copyChips = [1, 5, 10, 25, 50, 100];
        foreach (int c in copyChips)
        {
            int countVal = c;
            var chip = new PillChip { Text = $"{countVal}", Size = new Size(34, 25), Margin = new Padding(0, 0, 4, 0) };
            chip.Click += (s, e) => { _numCopies.Value = countVal; };
            pnlCopies.Controls.Add(chip);
        }
        colSettings.Controls.Add(pnlCopies);
        sy += 38;

        // PRINT BUTTON
        _btnPrint = new ModernButton
        {
            Text = "🖨️  PRINT LABELS NOW (F5)",
            DefaultBg = ModernColors.Primary,
            HoverBg = ModernColors.PrimaryHover,
            Width = 300,
            Height = 44,
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            Location = new Point(12, sy)
        };
        _btnPrint.Click += (s, e) => ExecutePrint();
        colSettings.Controls.Add(_btnPrint);

        colSettings.Controls.Add(lblCol2);

        // Column 3: Live Realistic WYSIWYG Preview (Right, remaining space)
        var colPreview = new ModernCard
        {
            Dock = DockStyle.Fill,
            CornerRadius = 14,
            Padding = new Padding(12)
        };

        // Header inside Preview Card (Title + Size Badge + Presets)
        var pnlPrevHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 56,
            BackColor = Color.Transparent
        };

        var lblCol3 = new Label
        {
            Text = "👁️ WYSIWYG Label Preview",
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            ForeColor = ModernColors.TextDark,
            Location = new Point(2, 2),
            AutoSize = true
        };

        _lblPreviewSize = new Label
        {
            Text = "40.0 mm × 45.0 mm @ 203 DPI",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = ModernColors.Primary,
            BackColor = ModernColors.PrimaryLight,
            Padding = new Padding(8, 4, 8, 4),
            Dock = DockStyle.Right,
            AutoSize = true
        };

        var pnlPreviewSizes = new FlowLayoutPanel
        {
            Location = new Point(2, 26),
            Dock = DockStyle.Bottom,
            Height = 28,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            Padding = new Padding(0),
            WrapContents = false
        };

        var lblQuick = new Label { Text = "Presets:", AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = ModernColors.TextMuted, Margin = new Padding(0, 4, 6, 0) };
        pnlPreviewSizes.Controls.Add(lblQuick);

        (string name, float w, float h)[] previewSizes = [("40×45 (4×4.5cm)", 40, 45), ("50×40", 50, 40), ("50×50", 50, 50), ("40×30", 40, 30), ("60×40", 60, 40)];
        foreach (var ps in previewSizes)
        {
            var pVal = ps;
            var chip = new PillChip { Text = pVal.name, Size = new Size(pVal.name.Length > 8 ? 96 : 48, 24), Margin = new Padding(0, 0, 4, 0) };
            chip.Click += (s, e) =>
            {
                if (_numStudioWidth != null && _numStudioHeight != null)
                {
                    _numStudioWidth.Value = (decimal)pVal.w;
                    _numStudioHeight.Value = (decimal)pVal.h;
                }
            };
            pnlPreviewSizes.Controls.Add(chip);
        }

        pnlPrevHeader.Controls.AddRange([lblCol3, _lblPreviewSize, pnlPreviewSizes]);

        _previewBox = new PictureBox
        {
            Location = new Point(12, 70),
            Width = colPreview.Width - 24,
            Height = Math.Max(120, colPreview.Height - 245),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = Color.FromArgb(242, 244, 250),
            SizeMode = PictureBoxSizeMode.Zoom
        };

        // Phrase Customizer Card inside Column 3
        var customizerPanel = new Panel
        {
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            Location = new Point(12, colPreview.Height - 165),
            Width = colPreview.Width - 24,
            Height = 140,
            BackColor = Color.FromArgb(248, 250, 253),
            Padding = new Padding(8, 6, 8, 6)
        };
        customizerPanel.Paint += (s, e) =>
        {
            using var pen = new Pen(ModernColors.Border, 1.2f);
            e.Graphics.DrawRectangle(pen, 0, 0, customizerPanel.Width - 1, customizerPanel.Height - 1);
        };

        var lblCustTitle = new Label
        {
            Text = "🎛️ Phrase Customizer (Position, Resize, Bold & Rotate Live)",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = ModernColors.TextDark,
            Dock = DockStyle.Top,
            Height = 20
        };

        // Row 1: Phrase Dropdown + Show Checkbox + Bold Checkbox + Rotation Controls
        var row1 = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 34,
            BackColor = Color.Transparent,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 4, 0, 2)
        };

        var lblPhrase = new Label { Text = "Phrase:", AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), Margin = new Padding(0, 4, 4, 0) };
        _cboPhraseSelector = new ComboBox
        {
            Width = 150,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 8.5f),
            Margin = new Padding(0, 2, 6, 0)
        };
        _cboPhraseSelector.Items.AddRange([
            "🏷️ English Title",
            "🏷️ Sinhala Title",
            "📅 M:DATE Row",
            "⏳ EXP:DATE Row",
            "🔢 BATCH NO Row",
            "💰 PRICE Row",
            "⚖️ Weight Row",
            "📝 Description",
            "📞 Telephone",
            "🏛️ Registration No (Side Text)"
        ]);
        _cboPhraseSelector.SelectedIndex = 0;
        _cboPhraseSelector.SelectedIndexChanged += (s, e) => OnSelectedPhraseChanged();

        _chkPhraseVisible = new CheckBox
        {
            Text = "Show",
            AutoSize = true,
            Checked = true,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Margin = new Padding(0, 4, 6, 0)
        };
        _chkPhraseVisible.CheckedChanged += (s, e) => OnPhraseControlChanged();

        _chkPhraseBold = new CheckBox
        {
            Text = "Bold (B)",
            AutoSize = true,
            Checked = true,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = ModernColors.TextDark,
            Margin = new Padding(0, 4, 10, 0)
        };
        _chkPhraseBold.CheckedChanged += (s, e) => OnPhraseControlChanged();

        var lblRotate = new Label { Text = "Rotation:", AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), Margin = new Padding(0, 4, 4, 0) };
        _cboPhraseRotation = new ComboBox
        {
            Width = 110,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 8.5f),
            Margin = new Padding(0, 2, 6, 0)
        };
        _cboPhraseRotation.Items.AddRange([
            "0° (Horizontal)",
            "90° (Downwards)",
            "180° (Inverted)",
            "270° (Upwards)"
        ]);
        _cboPhraseRotation.SelectedIndex = 0;
        _cboPhraseRotation.SelectedIndexChanged += (s, e) => OnPhraseControlChanged();

        _btnRotatePhrase = new ModernButton
        {
            Text = "🔄 90°",
            Width = 60,
            Height = 24,
            DefaultBg = Color.FromArgb(238, 242, 255),
            HoverBg = Color.FromArgb(224, 231, 255),
            ForeColor = ModernColors.Primary,
            Font = new Font("Segoe UI", 8f, FontStyle.Bold),
            Margin = new Padding(0, 2, 0, 0)
        };
        _btnRotatePhrase.Click += (s, e) =>
        {
            if (_cboPhraseRotation == null) return;
            _cboPhraseRotation.SelectedIndex = (_cboPhraseRotation.SelectedIndex + 1) % 4;
        };

        row1.Controls.AddRange([lblPhrase, _cboPhraseSelector, _chkPhraseVisible, _chkPhraseBold, lblRotate, _cboPhraseRotation, _btnRotatePhrase]);

        // Row 2: Font Size + Computer Font Selector + Nudge Buttons
        var row2 = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 34,
            BackColor = Color.Transparent,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 4, 0, 2)
        };

        var lblFontSz = new Label { Text = "Font Size:", AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), Margin = new Padding(0, 4, 4, 0) };
        var btnMinusFont = new ModernButton { Text = "-", Width = 22, Height = 23, DefaultBg = Color.FromArgb(226, 232, 240), HoverBg = Color.FromArgb(203, 213, 225), ForeColor = ModernColors.TextDark, Font = new Font("Segoe UI", 9f, FontStyle.Bold), Margin = new Padding(0, 2, 2, 0) };
        _numPhraseFontSize = new NumericUpDown { Width = 48, Minimum = 3, Maximum = 36, DecimalPlaces = 1, Value = 9.5m, Font = new Font("Segoe UI", 9f), Margin = new Padding(0, 2, 2, 0) };
        var btnPlusFont = new ModernButton { Text = "+", Width = 22, Height = 23, DefaultBg = Color.FromArgb(226, 232, 240), HoverBg = Color.FromArgb(203, 213, 225), ForeColor = ModernColors.TextDark, Font = new Font("Segoe UI", 9f, FontStyle.Bold), Margin = new Padding(0, 2, 8, 0) };

        btnMinusFont.Click += (s, e) => { _numPhraseFontSize.Value = Math.Max(3m, _numPhraseFontSize.Value - 0.5m); };
        btnPlusFont.Click += (s, e) => { _numPhraseFontSize.Value = Math.Min(36m, _numPhraseFontSize.Value + 0.5m); };
        _numPhraseFontSize.ValueChanged += (s, e) => OnPhraseControlChanged();

        var lblFont = new Label { Text = "Font:", AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), Margin = new Padding(0, 4, 4, 0) };
        _cboPhraseFont = new ComboBox { Width = 115, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 8.5f), Margin = new Padding(0, 2, 10, 0) };
        _cboPhraseFont.Items.Add("(Auto Font)");
        _cboPhraseFont.Items.AddRange(FontManager.GetInstalledFontNames().ToArray());
        _cboPhraseFont.SelectedIndex = 0;
        _cboPhraseFont.SelectedIndexChanged += (s, e) => OnPhraseControlChanged();

        var lblPos = new Label { Text = "Nudge:", AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), Margin = new Padding(0, 4, 4, 0) };
        var btnUp = new ModernButton { Text = "▲", Width = 30, Height = 23, DefaultBg = Color.FromArgb(241, 245, 249), HoverBg = Color.FromArgb(226, 232, 240), ForeColor = ModernColors.TextDark, Font = new Font("Segoe UI", 8f, FontStyle.Bold), Margin = new Padding(0, 2, 2, 0) };
        var btnDown = new ModernButton { Text = "▼", Width = 30, Height = 23, DefaultBg = Color.FromArgb(241, 245, 249), HoverBg = Color.FromArgb(226, 232, 240), ForeColor = ModernColors.TextDark, Font = new Font("Segoe UI", 8f, FontStyle.Bold), Margin = new Padding(0, 2, 2, 0) };
        var btnLeft = new ModernButton { Text = "◄", Width = 30, Height = 23, DefaultBg = Color.FromArgb(241, 245, 249), HoverBg = Color.FromArgb(226, 232, 240), ForeColor = ModernColors.TextDark, Font = new Font("Segoe UI", 8f, FontStyle.Bold), Margin = new Padding(0, 2, 2, 0) };
        var btnRight = new ModernButton { Text = "►", Width = 30, Height = 23, DefaultBg = Color.FromArgb(241, 245, 249), HoverBg = Color.FromArgb(226, 232, 240), ForeColor = ModernColors.TextDark, Font = new Font("Segoe UI", 8f, FontStyle.Bold), Margin = new Padding(0, 2, 0, 0) };

        _numPhraseOffsetY = new NumericUpDown { Visible = false, Minimum = -50, Maximum = 50, DecimalPlaces = 1 };
        _numPhraseOffsetX = new NumericUpDown { Visible = false, Minimum = -50, Maximum = 50, DecimalPlaces = 1 };
        _numPhraseOffsetY.ValueChanged += (s, e) => OnPhraseControlChanged();
        _numPhraseOffsetX.ValueChanged += (s, e) => OnPhraseControlChanged();

        btnUp.Click += (s, e) => { _numPhraseOffsetY.Value = Math.Max(-50m, _numPhraseOffsetY.Value - 0.5m); };
        btnDown.Click += (s, e) => { _numPhraseOffsetY.Value = Math.Min(50m, _numPhraseOffsetY.Value + 0.5m); };
        btnLeft.Click += (s, e) => { _numPhraseOffsetX.Value = Math.Max(-50m, _numPhraseOffsetX.Value - 0.5m); };
        btnRight.Click += (s, e) => { _numPhraseOffsetX.Value = Math.Min(50m, _numPhraseOffsetX.Value + 0.5m); };

        row2.Controls.AddRange([lblFontSz, btnMinusFont, _numPhraseFontSize, btnPlusFont, lblFont, _cboPhraseFont, lblPos, btnUp, btnDown, btnLeft, btnRight]);

        // Row 3: Action Buttons & Live Offset Status
        var row3 = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 34,
            BackColor = Color.Transparent,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 4, 0, 0)
        };

        var btnResetPhrase = new ModernButton
        {
            Text = "↺ Reset",
            Width = 62,
            Height = 25,
            DefaultBg = Color.FromArgb(254, 242, 242),
            HoverBg = Color.FromArgb(254, 226, 226),
            ForeColor = ModernColors.Danger,
            Font = new Font("Segoe UI", 8f, FontStyle.Bold),
            Margin = new Padding(0, 2, 6, 0)
        };
        btnResetPhrase.Click += (s, e) => ResetSelectedPhrase();

        var btnSaveLayout = new ModernButton
        {
            Text = "💾 Save Layout",
            Width = 115,
            Height = 25,
            DefaultBg = ModernColors.Primary,
            HoverBg = ModernColors.PrimaryHover,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Margin = new Padding(0, 2, 10, 0)
        };
        btnSaveLayout.Click += (s, e) => SaveCurrentLayoutToTemplate();

        _lblPhraseOffsetStatus = new Label
        {
            Text = "X: 0.0 mm | Y: 0.0 mm | 🔄 0° | 𝐁 Bold",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = ModernColors.Primary,
            AutoSize = true,
            Margin = new Padding(0, 6, 0, 0)
        };

        row3.Controls.AddRange([btnResetPhrase, btnSaveLayout, _lblPhraseOffsetStatus]);

        customizerPanel.Controls.AddRange([
            row3,
            row2,
            row1,
            lblCustTitle,
            _numPhraseOffsetX, _numPhraseOffsetY
        ]);

        _lblStatus = new Label
        {
            Text = "✓ Ready to print",
            Font = new Font("Segoe UI", 9f),
            ForeColor = ModernColors.Success,
            Dock = DockStyle.Bottom,
            Height = 20,
            TextAlign = ContentAlignment.MiddleLeft
        };

        colPreview.Controls.AddRange([pnlPrevHeader, _previewBox, customizerPanel, _lblStatus]);

        // Dynamically adjust preview layout on size changes
        colPreview.Resize += (s, e) =>
        {
            if (colPreview.Height > 250)
            {
                customizerPanel.Location = new Point(12, colPreview.Height - 165);
                customizerPanel.Width = colPreview.Width - 24;
                _previewBox.Location = new Point(12, 70);
                _previewBox.Width = colPreview.Width - 24;
                _previewBox.Height = Math.Max(100, customizerPanel.Top - 78);
            }
        };

        workPanel.Controls.Add(colPreview);
        workPanel.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 12, BackColor = Color.Transparent });
        workPanel.Controls.Add(colSettings);
        workPanel.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 12, BackColor = Color.Transparent });
        workPanel.Controls.Add(colProducts);

        _printStudioView.Controls.Add(workPanel);
        _printStudioView.Controls.Add(banner);
        if (trialBanner != null)
        {
            _printStudioView.Controls.Add(trialBanner);
        }

        PopulateStudioProducts();
        PopulateStudioTemplates();
    }

    private void PopulateStudioProducts()
    {
        _studioProductList.Items.Clear();
        string q = _studioSearchBox.Text.Trim();

        var list = string.IsNullOrEmpty(q)
            ? _store.Products
            : _store.Products.Where(p => p.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                                         p.SinhalaName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                                         p.Barcode.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();

        foreach (var p in list)
        {
            var item = new ListViewItem(p.Name) { Tag = p };
            item.SubItems.Add(p.DisplayPrice);
            _studioProductList.Items.Add(item);
        }

        if (_studioProductList.Items.Count > 0 && _studioProductList.SelectedItems.Count == 0)
        {
            _studioProductList.Items[0].Selected = true;
        }
    }

    private void FilterStudioProducts()
    {
        PopulateStudioProducts();
    }

    private void PopulateStudioTemplates()
    {
        _cboStudioTemplate.Items.Clear();
        foreach (var t in _store.Templates)
        {
            _cboStudioTemplate.Items.Add(t);
        }
        _cboStudioTemplate.DisplayMember = "Name";
        if (_cboStudioTemplate.Items.Count > 0)
        {
            _cboStudioTemplate.SelectedIndex = 0;
        }
    }

    private void OnStudioProductSelected(object? sender, EventArgs e)
    {
        if (_studioProductList.SelectedItems.Count == 0) return;
        var p = (ProductItem)_studioProductList.SelectedItems[0].Tag!;
        SelectProductForPrinting(p);
    }

    private void SelectProductForPrinting(ProductItem p)
    {
        _selectedProduct = p;
        _txtStudioName.Text = p.Name;
        _txtStudioSinhalaName.Text = p.SinhalaName;
        _txtStudioPrice.Text = p.DisplayPrice;
        _txtStudioWeight.Text = p.Weight;
        _txtStudioDesc.Text = p.Description;
        _txtStudioPhone.Text = !string.IsNullOrWhiteSpace(p.Tel) ? p.Tel : _store.Settings.CompanyPhone;
        _txtStudioReg.Text = p.RegistrationNo;

        // Auto batch number
        string prefix = !string.IsNullOrWhiteSpace(p.BatchPrefix) ? p.BatchPrefix : DateTime.Today.ToString("MM");
        _txtStudioBatch.Text = $"{prefix}{DateTime.Today:dd}";

        _dtpMDate.Value = DateTime.Today;
        _dtpExpDate.Value = DateTime.Today.AddDays(p.ShelfLifeDays > 0 ? p.ShelfLifeDays : 30);

        // Select preferred template if specified
        if (!string.IsNullOrEmpty(p.PreferredTemplateId))
        {
            for (int i = 0; i < _cboStudioTemplate.Items.Count; i++)
            {
                if (_cboStudioTemplate.Items[i] is LabelTemplate t && t.Id == p.PreferredTemplateId)
                {
                    _cboStudioTemplate.SelectedIndex = i;
                    break;
                }
            }
        }

        RefreshStudioPreview();
    }

    private void RefreshStudioPreview()
    {
        if (_previewBox == null) return;

        UpdateCurrentJob();
        _lblPreviewSize.Text = $"{_currentJob.Template.WidthMm:0.#} mm × {_currentJob.Template.HeightMm:0.#} mm @ {_currentJob.Template.Dpi} DPI";

        try
        {
            int w = Math.Max(200, _previewBox.Width);
            int h = Math.Max(150, _previewBox.Height);
            var bmp = LabelRenderer.RenderPreview(_currentJob, w, h);
            _previewBox.Image?.Dispose();
            _previewBox.Image = bmp;
        }
        catch { }
    }

    private void UpdateCurrentJob()
    {
        _currentJob.ProductName = _txtStudioName.Text.Trim();
        _currentJob.SinhalaName = _txtStudioSinhalaName.Text.Trim();
        _currentJob.ManufactureDate = _dtpMDate.Value;
        _currentJob.ExpiryDate = _dtpExpDate.Value;
        _currentJob.BatchNumber = _txtStudioBatch.Text.Trim();
        _currentJob.PriceText = _txtStudioPrice.Text.Trim();
        _currentJob.WeightText = _txtStudioWeight.Text.Trim();
        _currentJob.Description = _txtStudioDesc.Text.Trim();
        _currentJob.Tel = _txtStudioPhone.Text.Trim();
        _currentJob.RegistrationNo = _txtStudioReg.Text.Trim();
        _currentJob.Barcode = _selectedProduct?.Barcode ?? _currentJob.BatchNumber;
        _currentJob.Copies = (int)_numCopies.Value;
        _currentJob.Template = _activeTemplate;
        if (_numStudioWidth != null && _numStudioHeight != null)
        {
            _currentJob.Template.WidthMm = (float)_numStudioWidth.Value;
            _currentJob.Template.HeightMm = (float)_numStudioHeight.Value;
        }
    }

    private ElementLayout GetSelectedElementLayout()
    {
        int idx = _cboPhraseSelector?.SelectedIndex ?? 0;
        return idx switch
        {
            0 => _activeTemplate.LayoutTitle,
            1 => _activeTemplate.LayoutSinhalaTitle,
            2 => _activeTemplate.LayoutMDate,
            3 => _activeTemplate.LayoutExpDate,
            4 => _activeTemplate.LayoutBatch,
            5 => _activeTemplate.LayoutPrice,
            6 => _activeTemplate.LayoutWeight,
            7 => _activeTemplate.LayoutDesc,
            8 => _activeTemplate.LayoutTel,
            9 => _activeTemplate.LayoutReg,
            _ => _activeTemplate.LayoutTitle
        };
    }

    private float GetDefaultFontSizeForIndex(int idx)
    {
        float scale = Math.Clamp(Math.Min(_activeTemplate.WidthMm / 40f, _activeTemplate.HeightMm / 45f), 0.5f, 2.5f);
        return idx switch
        {
            0 => Math.Max(7.0f, (_activeTemplate.TitleFontSize > 0 ? _activeTemplate.TitleFontSize : 9.5f) * scale),
            1 => Math.Max(6.5f, 8.0f * scale),
            2 => Math.Max(5.2f, (_activeTemplate.BodyFontSize > 0 ? _activeTemplate.BodyFontSize : 6.2f) * scale),
            3 => Math.Max(5.2f, (_activeTemplate.BodyFontSize > 0 ? _activeTemplate.BodyFontSize : 6.2f) * scale),
            4 => Math.Max(5.2f, (_activeTemplate.BodyFontSize > 0 ? _activeTemplate.BodyFontSize : 6.2f) * scale),
            5 => Math.Max(5.2f, (_activeTemplate.BodyFontSize > 0 ? _activeTemplate.BodyFontSize : 6.2f) * scale),
            6 => Math.Max(5.2f, (_activeTemplate.BodyFontSize > 0 ? _activeTemplate.BodyFontSize : 6.2f) * scale),
            7 => Math.Max(4.5f, 5.0f * scale),
            8 => Math.Max(5.5f, 6.8f * scale),
            9 => Math.Max(4.2f, 4.8f * scale),
            _ => 7.0f
        };
    }

    private bool GetDefaultBoldForIndex(int idx) => idx switch
    {
        7 => false, // Description default is regular
        9 => false, // Registration side text default is regular
        _ => true   // Titles, Metadata rows, Telephone default is bold
    };

    private void OnSelectedPhraseChanged()
    {
        if (_cboPhraseSelector == null || _numPhraseFontSize == null) return;
        var layout = GetSelectedElementLayout();
        _isUpdatingPhraseControls = true;
        try
        {
            float currentPt = layout.FontSize > 0 ? layout.FontSize : GetDefaultFontSizeForIndex(_cboPhraseSelector.SelectedIndex);
            _numPhraseFontSize.Value = (decimal)Math.Clamp(currentPt, 3f, 36f);
            _numPhraseOffsetX.Value = (decimal)Math.Clamp(layout.OffsetXMm, -50f, 50f);
            _numPhraseOffsetY.Value = (decimal)Math.Clamp(layout.OffsetYMm, -50f, 50f);
            _chkPhraseVisible.Checked = layout.Visible;
            if (_chkPhraseBold != null)
            {
                _chkPhraseBold.Checked = layout.Bold ?? GetDefaultBoldForIndex(_cboPhraseSelector.SelectedIndex);
            }

            int rotIdx = layout.RotationDeg switch
            {
                90 => 1,
                180 => 2,
                270 => 3,
                _ => 0
            };
            if (_cboPhraseRotation != null && _cboPhraseRotation.Items.Count > rotIdx)
            {
                _cboPhraseRotation.SelectedIndex = rotIdx;
            }

            if (!string.IsNullOrEmpty(layout.FontFamily) && _cboPhraseFont.Items.Contains(layout.FontFamily))
            {
                _cboPhraseFont.SelectedItem = layout.FontFamily;
            }
            else
            {
                _cboPhraseFont.SelectedIndex = 0; // (Auto Font)
            }

            UpdatePhraseStatusLabel();
        }
        finally
        {
            _isUpdatingPhraseControls = false;
        }
    }

    private void OnPhraseControlChanged()
    {
        if (_isUpdatingPhraseControls || _cboPhraseSelector == null) return;
        var layout = GetSelectedElementLayout();
        layout.FontSize = (float)_numPhraseFontSize.Value;
        layout.OffsetXMm = (float)_numPhraseOffsetX.Value;
        layout.OffsetYMm = (float)_numPhraseOffsetY.Value;
        layout.Visible = _chkPhraseVisible.Checked;
        if (_chkPhraseBold != null)
        {
            layout.Bold = _chkPhraseBold.Checked;
        }
        layout.FontFamily = _cboPhraseFont.SelectedIndex > 0 ? _cboPhraseFont.SelectedItem?.ToString() ?? "" : "";

        if (_cboPhraseRotation != null)
        {
            layout.RotationDeg = _cboPhraseRotation.SelectedIndex switch
            {
                1 => 90,
                2 => 180,
                3 => 270,
                _ => 0
            };
        }

        UpdatePhraseStatusLabel();
        RefreshStudioPreview();
    }

    private void ResetSelectedPhrase()
    {
        var layout = GetSelectedElementLayout();
        layout.FontSize = 0f;
        layout.OffsetXMm = 0f;
        layout.OffsetYMm = 0f;
        layout.Visible = true;
        layout.Bold = null;
        layout.FontFamily = "";
        layout.RotationDeg = (_cboPhraseSelector?.SelectedIndex == 9) ? 90 : 0;

        OnSelectedPhraseChanged();
        RefreshStudioPreview();
    }

    private void UpdatePhraseStatusLabel()
    {
        if (_lblPhraseOffsetStatus == null) return;
        var layout = GetSelectedElementLayout();
        bool isBold = layout.Bold ?? GetDefaultBoldForIndex(_cboPhraseSelector?.SelectedIndex ?? 0);
        string boldStr = isBold ? "𝐁 Bold" : "Regular";
        _lblPhraseOffsetStatus.Text = $"X: {layout.OffsetXMm:+0.0;-0.0;0.0} mm | Y: {layout.OffsetYMm:+0.0;-0.0;0.0} mm | 🔄 {layout.RotationDeg}° | {boldStr}";
    }

    private void SaveCurrentLayoutToTemplate()
    {
        if (_numStudioWidth != null && _numStudioHeight != null)
        {
            _activeTemplate.WidthMm = (float)_numStudioWidth.Value;
            _activeTemplate.HeightMm = (float)_numStudioHeight.Value;
        }
        _store.AddOrUpdateTemplate(_activeTemplate);
        MessageBox.Show($"Custom phrase positioning and font sizes saved to template '{_activeTemplate.Name}'!", "Layout Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ExecutePrint()
    {
        if (_store.Settings.ConnectionType == PrinterConnectionType.BluetoothSerial)
        {
            if (string.IsNullOrWhiteSpace(_store.Settings.BluetoothPort) && _cboBluetoothPort?.SelectedItem is BluetoothPortInfo bt)
            {
                _store.Settings.BluetoothPort = bt.PortName;
                _store.SaveSettings();
            }

            if (string.IsNullOrWhiteSpace(_store.Settings.BluetoothPort))
            {
                MessageBox.Show("Please select an active Bluetooth / COM port in Settings before printing.", "No Bluetooth Port Selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                SwitchTab("settings");
                return;
            }
        }
        else
        {
            string printer = _store.Settings.SelectedPrinter;
            if (string.IsNullOrWhiteSpace(printer) && _cboSettingsPrinter?.SelectedItem != null)
            {
                printer = _cboSettingsPrinter.SelectedItem.ToString() ?? "";
                _store.Settings.SelectedPrinter = printer;
                _store.SaveSettings();
            }

            if (string.IsNullOrWhiteSpace(printer) && PrinterSettings.InstalledPrinters.Count > 0)
            {
                printer = new PrinterSettings().PrinterName;
                _store.Settings.SelectedPrinter = printer;
                _store.SaveSettings();
            }

            if (string.IsNullOrWhiteSpace(printer))
            {
                MessageBox.Show("Please select a printer in Settings before printing.", "No Printer Selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                SwitchTab("settings");
                return;
            }
        }

        if (!TrialManager.CheckAccessAndShowDialog(this))
        {
            _btnPrint.Enabled = false;
            return;
        }

        if (AppConfig.IsTrial && (int)_numCopies.Value > 3)
        {
            _numCopies.Value = 3;
            MessageBox.Show("Trial version is limited to a maximum of 3 test copies per print.", "Trial Notice", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        UpdateCurrentJob();
        _btnPrint.Enabled = false;
        _lblStatus.Text = "⏳ Printing labels...";
        _lblStatus.ForeColor = ModernColors.Warning;

        string targetName = _store.Settings.ConnectionType == PrinterConnectionType.BluetoothSerial
            ? $"Bluetooth ({_store.Settings.BluetoothPort})"
            : _store.Settings.SelectedPrinter;

        Task.Run(() =>
        {
            try
            {
                PrintingEngine.Print(_currentJob, _store.Settings);
                _store.AddHistory(_currentJob, targetName);

                Invoke(() =>
                {
                    _btnPrint.Enabled = true;
                    _lblStatus.Text = $"✓ Printed {_currentJob.Copies} label(s) successfully → {targetName}";
                    _lblStatus.ForeColor = ModernColors.Success;
                });
            }
            catch (Exception ex)
            {
                Invoke(() =>
                {
                    _btnPrint.Enabled = true;
                    _lblStatus.Text = $"✗ Print Error: {ex.Message}";
                    _lblStatus.ForeColor = ModernColors.Danger;
                    MessageBox.Show($"Failed to print label:\n\n{ex.Message}", "Printer Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                });
            }
        });
    }

    private void UpdateTrialUiCountdown()
    {
        if (!AppConfig.IsTrial) return;

        var rem = TrialManager.RemainingTime;
        if (rem <= TimeSpan.Zero || TrialManager.IsTrialExpired)
        {
            _trialUiTimer?.Stop();
            OnTrialExpiredHandler();
            return;
        }

        string timeStr = $"{rem.Minutes}m {rem.Seconds:D2}s";
        if (_lblHeaderTrialBadge != null)
        {
            _lblHeaderTrialBadge.Text = $"⚠️ TRIAL ({timeStr})";
        }
        if (_lblTrialBanner != null)
        {
            _lblTrialBanner.Text = $"⚠️ 30-MINUTE TRIAL EVALUATION: {timeStr} remaining on this device. Permanent watermarks are applied to all stickers.";
        }
    }

    private void OnTrialExpiredHandler()
    {
        if (InvokeRequired)
        {
            Invoke(OnTrialExpiredHandler);
            return;
        }

        _btnPrint.Enabled = false;
        _lblStatus.Text = "❌ Trial period expired on this device. Printing disabled.";
        _lblStatus.ForeColor = ModernColors.Danger;
        if (_lblHeaderTrialBadge != null)
        {
            _lblHeaderTrialBadge.Text = "🔒 EXPIRED";
        }
        if (_lblTrialBanner != null)
        {
            _lblTrialBanner.Text = "🔒 TRIAL EXPIRED: 30-minute evaluation period has ended on this device. Printing disabled.";
        }

        TrialManager.CheckAccessAndShowDialog(this);
    }

    #endregion

    #region Products Catalog View

    private void BuildProductsView()
    {
        _productsView = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };

        var topBar = new ModernCard { Dock = DockStyle.Top, Height = 64, CornerRadius = 14, Padding = new Padding(12) };

        _productSearchBox = new TextBox
        {
            Location = new Point(14, 16),
            Width = 320,
            Font = new Font("Segoe UI", 10f),
            PlaceholderText = "🔍 Search products by name, code, Sinhala name..."
        };
        _productSearchBox.TextChanged += (s, e) => RefreshProductsTable();

        var btnAdd = new ModernButton
        {
            Text = "+ Add New Product",
            DefaultBg = ModernColors.Primary,
            HoverBg = ModernColors.PrimaryHover,
            Width = 160,
            Height = 36,
            Location = new Point(350, 14)
        };
        btnAdd.Click += (s, e) => OpenProductEditDialog(null);

        var btnEdit = new ModernButton
        {
            Text = "✏️ Edit Product",
            DefaultBg = Color.FromArgb(241, 245, 249),
            HoverBg = Color.FromArgb(226, 232, 240),
            ForeColor = ModernColors.TextDark,
            Width = 130,
            Height = 36,
            Location = new Point(520, 14)
        };
        btnEdit.Click += (s, e) =>
        {
            if (_productsTable.SelectedItems.Count > 0 && _productsTable.SelectedItems[0].Tag is ProductItem p)
                OpenProductEditDialog(p);
        };

        var btnDelete = new ModernButton
        {
            Text = "🗑️ Delete",
            DefaultBg = Color.FromArgb(254, 242, 242),
            HoverBg = Color.FromArgb(254, 226, 226),
            ForeColor = ModernColors.Danger,
            Width = 100,
            Height = 36,
            Location = new Point(660, 14)
        };
        btnDelete.Click += (s, e) =>
        {
            if (_productsTable.SelectedItems.Count > 0 && _productsTable.SelectedItems[0].Tag is ProductItem p)
            {
                if (MessageBox.Show($"Are you sure you want to delete '{p.Name}'?", "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    _store.DeleteProduct(p.Id);
                    RefreshProductsTable();
                    PopulateStudioProducts();
                }
            }
        };

        var btnPrintSelected = new ModernButton
        {
            Text = "🖨️ Print Label",
            DefaultBg = ModernColors.Success,
            HoverBg = Color.FromArgb(5, 150, 105),
            Width = 130,
            Height = 36,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(topBar.Width - 150, 14)
        };
        btnPrintSelected.Click += (s, e) =>
        {
            if (_productsTable.SelectedItems.Count > 0 && _productsTable.SelectedItems[0].Tag is ProductItem p)
            {
                SelectProductForPrinting(p);
                SwitchTab("studio");
            }
        };

        topBar.Controls.AddRange([_productSearchBox, btnAdd, btnEdit, btnDelete, btnPrintSelected]);

        var tableCard = new ModernCard { Dock = DockStyle.Fill, CornerRadius = 14, Padding = new Padding(12) };
        _productsTable = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            BorderStyle = BorderStyle.None,
            Font = new Font("Segoe UI", 9.5f)
        };
        _productsTable.Columns.Add("Product Name", 190);
        _productsTable.Columns.Add("Sinhala Name (සිංහල)", 160);
        _productsTable.Columns.Add("Category", 130);
        _productsTable.Columns.Add("Default Price", 100);
        _productsTable.Columns.Add("Weight", 100);
        _productsTable.Columns.Add("Shelf Life", 90);
        _productsTable.Columns.Add("Barcode / Code", 120);

        _productsTable.DoubleClick += (s, e) =>
        {
            if (_productsTable.SelectedItems.Count > 0 && _productsTable.SelectedItems[0].Tag is ProductItem p)
                OpenProductEditDialog(p);
        };

        tableCard.Controls.Add(_productsTable);

        _productsView.Controls.Add(tableCard);
        _productsView.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 12, BackColor = Color.Transparent });
        _productsView.Controls.Add(topBar);
    }

    private void RefreshProductsTable()
    {
        _productsTable.Items.Clear();
        string q = _productSearchBox.Text.Trim();

        var list = string.IsNullOrEmpty(q)
            ? _store.Products
            : _store.Products.Where(p => p.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                                         p.SinhalaName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                                         p.Category.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                                         p.Barcode.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();

        foreach (var p in list)
        {
            var item = new ListViewItem(p.Name) { Tag = p };
            item.SubItems.Add(p.SinhalaName);
            item.SubItems.Add(p.Category);
            item.SubItems.Add(p.DisplayPrice);
            item.SubItems.Add(p.Weight);
            item.SubItems.Add($"{p.ShelfLifeDays} days");
            item.SubItems.Add(p.Barcode);
            _productsTable.Items.Add(item);
        }
    }

    private void OpenProductEditDialog(ProductItem? existing)
    {
        using var dlg = new Form
        {
            Text = existing == null ? "Add New Product" : $"Edit Product - {existing.Name}",
            Size = new Size(540, 680),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = Color.White,
            Font = new Font("Segoe UI", 9.5f)
        };

        int y = 16;
        void AddLabel(string text)
        {
            dlg.Controls.Add(new Label { Text = text, Location = new Point(20, y), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = ModernColors.TextDark });
            y += 20;
        }

        AddLabel("Product Name (English)");
        var txtName = new TextBox { Location = new Point(20, y), Width = 480, Text = existing?.Name ?? "" };
        dlg.Controls.Add(txtName); y += 32;

        AddLabel("Sinhala Name (සිංහල නම)");
        var txtSinhala = new TextBox { Location = new Point(20, y), Width = 480, Text = existing?.SinhalaName ?? "", Font = new Font("Nirmala UI", 9.5f, FontStyle.Bold) };
        dlg.Controls.Add(txtSinhala); y += 32;

        AddLabel("Category");
        var txtCat = new TextBox { Location = new Point(20, y), Width = 480, Text = existing?.Category ?? "Bakery & Biscuits" };
        dlg.Controls.Add(txtCat); y += 32;

        // Price and Weight row
        dlg.Controls.Add(new Label { Text = "Default Price (Number)", Location = new Point(20, y), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) });
        dlg.Controls.Add(new Label { Text = "Default Weight (e.g. 360-380g)", Location = new Point(260, y), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) });
        y += 20;

        var numPrice = new NumericUpDown { Location = new Point(20, y), Width = 230, Maximum = 999999, DecimalPlaces = 0, Value = (decimal)(existing?.DefaultPrice ?? 260) };
        var txtWeight = new TextBox { Location = new Point(260, y), Width = 240, Text = existing?.Weight ?? "360-380g" };
        dlg.Controls.AddRange([numPrice, txtWeight]); y += 32;

        // Shelf Life & Batch Prefix row
        dlg.Controls.Add(new Label { Text = "Shelf Life (Days)", Location = new Point(20, y), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) });
        dlg.Controls.Add(new Label { Text = "Batch Prefix (Optional)", Location = new Point(260, y), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) });
        y += 20;

        var numShelf = new NumericUpDown { Location = new Point(20, y), Width = 230, Maximum = 3650, Value = existing?.ShelfLifeDays ?? 30 };
        var txtBatchPrefix = new TextBox { Location = new Point(260, y), Width = 240, Text = existing?.BatchPrefix ?? "11" };
        dlg.Controls.AddRange([numShelf, txtBatchPrefix]); y += 32;

        AddLabel("Description / Ingredients (Sinhala / English)");
        var txtDesc = new TextBox { Location = new Point(20, y), Width = 480, Height = 60, Multiline = true, Text = existing?.Description ?? "තුනී තැටි රෝල් කෙටි කෑමක් රසැති නිශ්පාදන කිසිදු කල්තබා ගැනීමේ රසායනික ද්‍රව්‍ය අඩංගු නැත.", Font = new Font("Nirmala UI", 8.5f) };
        dlg.Controls.Add(txtDesc); y += 68;

        AddLabel("Registration Number (දි:ව ලියාපදිංචි)");
        var txtReg = new TextBox { Location = new Point(20, y), Width = 480, Text = existing?.RegistrationNo ?? "දි:ව ලියාපදිංචි -    2/1/85/2022-02-25/17", Font = new Font("Nirmala UI", 8.5f) };
        dlg.Controls.Add(txtReg); y += 32;

        AddLabel("Contact Phone (e.g. Tel-070-2220111)");
        var txtTel = new TextBox { Location = new Point(20, y), Width = 480, Text = existing?.Tel ?? "Tel-070-2220111" };
        dlg.Controls.Add(txtTel); y += 32;

        AddLabel("Barcode / SKU");
        var txtBarcode = new TextBox { Location = new Point(20, y), Width = 480, Text = existing?.Barcode ?? "112100260" };
        dlg.Controls.Add(txtBarcode); y += 40;

        var btnSave = new ModernButton
        {
            Text = "Save Product",
            DefaultBg = ModernColors.Primary,
            HoverBg = ModernColors.PrimaryHover,
            Width = 140,
            Height = 38,
            Location = new Point(220, y)
        };
        var btnCancel = new ModernButton
        {
            Text = "Cancel",
            DefaultBg = Color.FromArgb(241, 245, 249),
            HoverBg = Color.FromArgb(226, 232, 240),
            ForeColor = ModernColors.TextDark,
            Width = 120,
            Height = 38,
            Location = new Point(380, y)
        };
        btnCancel.Click += (s, e) => dlg.Close();

        btnSave.Click += (s, e) =>
        {
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                MessageBox.Show("Please enter a product name.", "Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var prod = existing ?? new ProductItem();
            prod.Name = txtName.Text.Trim();
            prod.SinhalaName = txtSinhala.Text.Trim();
            prod.Category = txtCat.Text.Trim();
            prod.DefaultPrice = (double)numPrice.Value;
            prod.Weight = txtWeight.Text.Trim();
            prod.ShelfLifeDays = (int)numShelf.Value;
            prod.BatchPrefix = txtBatchPrefix.Text.Trim();
            prod.Description = txtDesc.Text.Trim();
            prod.RegistrationNo = txtReg.Text.Trim();
            prod.Tel = txtTel.Text.Trim();
            prod.Barcode = txtBarcode.Text.Trim();

            _store.AddOrUpdateProduct(prod);
            RefreshProductsTable();
            PopulateStudioProducts();
            dlg.Close();
        };

        dlg.Controls.AddRange([btnSave, btnCancel]);
        dlg.ShowDialog(this);
    }

    #endregion

    #region Templates Manager View

    private void BuildTemplatesView()
    {
        _templatesView = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterDistance = 280,
            BorderStyle = BorderStyle.None
        };

        // Left: Templates List
        var listCard = new ModernCard { Dock = DockStyle.Fill, CornerRadius = 14, Padding = new Padding(12) };
        var lblList = new Label { Text = "📐 Saved Templates", Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), Location = new Point(10, 10), AutoSize = true };

        _templateList = new ListView
        {
            Location = new Point(10, 38),
            Width = 260,
            Height = 560,
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            View = View.Details,
            FullRowSelect = true,
            HeaderStyle = ColumnHeaderStyle.None,
            BorderStyle = BorderStyle.None
        };
        _templateList.Columns.Add("Name", 240);
        _templateList.SelectedIndexChanged += OnTemplateSelectedInManager;

        var btnNewTpl = new ModernButton
        {
            Text = "+ Create New Template",
            DefaultBg = ModernColors.Primary,
            HoverBg = ModernColors.PrimaryHover,
            Width = 260,
            Height = 36,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            Location = new Point(10, listCard.Height - 48)
        };
        btnNewTpl.Click += (s, e) => CreateNewTemplate();

        listCard.Controls.AddRange([lblList, _templateList, btnNewTpl]);
        split.Panel1.Controls.Add(listCard);

        // Right: Template Editor & Live Preview
        var editCard = new ModernCard { Dock = DockStyle.Fill, CornerRadius = 14, Padding = new Padding(16) };

        var lblEdit = new Label { Text = "✏️ Template Settings & Field Configuration", Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), Location = new Point(12, 10), AutoSize = true };

        int y = 40;
        AddFormLabel(editCard, "Template Name", 12, ref y);
        _txtTplName = AddFormTextBox(editCard, 12, ref y, 320);

        // Dimensions row
        editCard.Controls.Add(new Label { Text = "Width (mm)", Location = new Point(12, y), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) });
        editCard.Controls.Add(new Label { Text = "Height (mm)", Location = new Point(120, y), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) });
        editCard.Controls.Add(new Label { Text = "Printer DPI", Location = new Point(230, y), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) });
        y += 20;

        _numTplWidth = new NumericUpDown { Location = new Point(12, y), Width = 96, Minimum = 10, Maximum = 300, Value = 50, DecimalPlaces = 1 };
        _numTplHeight = new NumericUpDown { Location = new Point(120, y), Width = 96, Minimum = 10, Maximum = 300, Value = 40, DecimalPlaces = 1 };
        _numTplDpi = new NumericUpDown { Location = new Point(230, y), Width = 100, Minimum = 72, Maximum = 600, Value = 203 };

        _numTplWidth.ValueChanged += (s, e) => RefreshTemplateDesignerPreview();
        _numTplHeight.ValueChanged += (s, e) => RefreshTemplateDesignerPreview();
        _numTplDpi.ValueChanged += (s, e) => RefreshTemplateDesignerPreview();

        editCard.Controls.AddRange([_numTplWidth, _numTplHeight, _numTplDpi]); y += 32;

        // Fonts Selection Row (ALL COMPUTER FONTS LOADED!)
        AddFormLabel(editCard, "Primary English Font (Installed on Computer)", 12, ref y);
        _cboTplFont = new ComboBox { Location = new Point(12, y), Width = 320, DropDownStyle = ComboBoxStyle.DropDownList };
        editCard.Controls.Add(_cboTplFont); y += 32;

        AddFormLabel(editCard, "Sinhala Font (Installed on Computer)", 12, ref y);
        _cboTplSinhalaFont = new ComboBox { Location = new Point(12, y), Width = 320, DropDownStyle = ComboBoxStyle.DropDownList };
        editCard.Controls.Add(_cboTplSinhalaFont); y += 32;

        // Populate Font ComboBoxes from computer
        var computerFonts = FontManager.GetInstalledFontNames();
        _cboTplFont.Items.AddRange(computerFonts.ToArray());
        _cboTplSinhalaFont.Items.AddRange(computerFonts.ToArray());

        _cboTplFont.SelectedIndexChanged += (s, e) => RefreshTemplateDesignerPreview();
        _cboTplSinhalaFont.SelectedIndexChanged += (s, e) => RefreshTemplateDesignerPreview();

        // Font Sizes
        editCard.Controls.Add(new Label { Text = "Title Font Size (pt)", Location = new Point(12, y), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) });
        editCard.Controls.Add(new Label { Text = "Body Font Size (pt)", Location = new Point(170, y), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) });
        y += 20;

        _numTitleFont = new NumericUpDown { Location = new Point(12, y), Width = 140, Minimum = 5, Maximum = 36, Value = 10, DecimalPlaces = 1 };
        _numBodyFont = new NumericUpDown { Location = new Point(170, y), Width = 160, Minimum = 4, Maximum = 24, Value = 7, DecimalPlaces = 1 };
        _numTitleFont.ValueChanged += (s, e) => RefreshTemplateDesignerPreview();
        _numBodyFont.ValueChanged += (s, e) => RefreshTemplateDesignerPreview();
        editCard.Controls.AddRange([_numTitleFont, _numBodyFont]); y += 32;

        // Field Toggles Checkboxes
        AddFormLabel(editCard, "Visible Fields on Label", 12, ref y);

        _chkSinhala = AddCheckbox(editCard, "Show Sinhala Name", 12, ref y);
        _chkMDate = AddCheckbox(editCard, "Show Manufacture Date (M:DATE)", 12, ref y);
        _chkExpDate = AddCheckbox(editCard, "Show Expiry Date (EXP : DATE)", 12, ref y);
        _chkBatch = AddCheckbox(editCard, "Show Batch Number (BATCH NO)", 12, ref y);
        _chkPrice = AddCheckbox(editCard, "Show Price (PRICE මිල)", 12, ref y);
        _chkWeight = AddCheckbox(editCard, "Show Weight (බර)", 12, ref y);
        _chkDesc = AddCheckbox(editCard, "Show Description / Ingredients", 12, ref y);
        _chkPhone = AddCheckbox(editCard, "Show Telephone / Contact", 12, ref y);
        _chkReg = AddCheckbox(editCard, "Show Registration No (දි:ව ලියාපදිංචි)", 12, ref y);
        _chkBarcode = AddCheckbox(editCard, "Show Barcode (Code 128)", 12, ref y);

        var btnSaveTpl = new ModernButton
        {
            Text = "💾 Save Template",
            DefaultBg = ModernColors.Primary,
            HoverBg = ModernColors.PrimaryHover,
            Width = 160,
            Height = 38,
            Location = new Point(12, y + 8)
        };
        btnSaveTpl.Click += (s, e) => SaveCurrentTemplateFromEditor();

        var btnDeleteTpl = new ModernButton
        {
            Text = "🗑️ Delete Template",
            DefaultBg = Color.FromArgb(254, 242, 242),
            HoverBg = Color.FromArgb(254, 226, 226),
            ForeColor = ModernColors.Danger,
            Width = 150,
            Height = 38,
            Location = new Point(180, y + 8)
        };
        btnDeleteTpl.Click += (s, e) => DeleteCurrentTemplate();

        editCard.Controls.AddRange([lblEdit, btnSaveTpl, btnDeleteTpl]);

        // Template Preview on the far right
        _templatePreviewBox = new PictureBox
        {
            Location = new Point(360, 40),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            Width = editCard.Width - 380,
            Height = editCard.Height - 60,
            BackColor = Color.FromArgb(242, 244, 250),
            SizeMode = PictureBoxSizeMode.Zoom
        };
        editCard.Controls.Add(_templatePreviewBox);

        split.Panel2.Controls.Add(editCard);
        _templatesView.Controls.Add(split);
    }

    private CheckBox AddCheckbox(Control parent, string text, int x, ref int y)
    {
        var chk = new CheckBox
        {
            Text = text,
            Location = new Point(x, y),
            AutoSize = true,
            Checked = true,
            Font = new Font("Segoe UI", 9f)
        };
        chk.CheckedChanged += (s, e) => RefreshTemplateDesignerPreview();
        parent.Controls.Add(chk);
        y += 24;
        return chk;
    }

    private void RefreshTemplatesList()
    {
        _templateList.Items.Clear();
        foreach (var tpl in _store.Templates)
        {
            var item = new ListViewItem(tpl.Name) { Tag = tpl };
            _templateList.Items.Add(item);
        }
        if (_templateList.Items.Count > 0 && _templateList.SelectedItems.Count == 0)
        {
            _templateList.Items[0].Selected = true;
        }
    }

    private void OnTemplateSelectedInManager(object? sender, EventArgs e)
    {
        if (_templateList.SelectedItems.Count == 0) return;
        var tpl = (LabelTemplate)_templateList.SelectedItems[0].Tag!;
        LoadTemplateIntoEditor(tpl);
    }

    private void LoadTemplateIntoEditor(LabelTemplate tpl)
    {
        _txtTplName.Text = tpl.Name;
        _numTplWidth.Value = (decimal)tpl.WidthMm;
        _numTplHeight.Value = (decimal)tpl.HeightMm;
        _numTplDpi.Value = tpl.Dpi;

        _cboTplFont.SelectedItem = tpl.FontFamily;
        if (_cboTplFont.SelectedIndex < 0 && _cboTplFont.Items.Count > 0) _cboTplFont.SelectedIndex = 0;

        _cboTplSinhalaFont.SelectedItem = tpl.SinhalaFontFamily;
        if (_cboTplSinhalaFont.SelectedIndex < 0 && _cboTplSinhalaFont.Items.Count > 0) _cboTplSinhalaFont.SelectedIndex = 0;

        _numTitleFont.Value = (decimal)tpl.TitleFontSize;
        _numBodyFont.Value = (decimal)tpl.BodyFontSize;

        _chkSinhala.Checked = tpl.ShowSinhalaName;
        _chkMDate.Checked = tpl.ShowManufactureDate;
        _chkExpDate.Checked = tpl.ShowExpiryDate;
        _chkBatch.Checked = tpl.ShowBatchNo;
        _chkPrice.Checked = tpl.ShowPrice;
        _chkWeight.Checked = tpl.ShowWeight;
        _chkDesc.Checked = tpl.ShowDescription;
        _chkPhone.Checked = tpl.ShowTel;
        _chkReg.Checked = tpl.ShowRegistration;
        _chkBarcode.Checked = tpl.ShowBarcode;

        RefreshTemplateDesignerPreview();
    }

    private void RefreshTemplateDesignerPreview()
    {
        if (_templatePreviewBox == null) return;

        var dummyProduct = _selectedProduct ?? DataStore.GetDefaultProducts()[0];
        var testJob = new PrintJob
        {
            ProductName = dummyProduct.Name,
            SinhalaName = dummyProduct.SinhalaName,
            PriceText = dummyProduct.DisplayPrice,
            WeightText = dummyProduct.Weight,
            Description = dummyProduct.Description,
            Tel = dummyProduct.Tel,
            RegistrationNo = dummyProduct.RegistrationNo,
            Barcode = dummyProduct.Barcode,
            ManufactureDate = DateTime.Today,
            ExpiryDate = DateTime.Today.AddDays(30),
            BatchNumber = "1121",
            Template = BuildTemplateFromEditor()
        };

        try
        {
            int w = Math.Max(200, _templatePreviewBox.Width);
            int h = Math.Max(150, _templatePreviewBox.Height);
            var bmp = LabelRenderer.RenderPreview(testJob, w, h);
            _templatePreviewBox.Image?.Dispose();
            _templatePreviewBox.Image = bmp;
        }
        catch { }
    }

    private LabelTemplate BuildTemplateFromEditor()
    {
        var tpl = (_templateList.SelectedItems.Count > 0 && _templateList.SelectedItems[0].Tag is LabelTemplate existing)
            ? existing
            : new LabelTemplate();

        tpl.Name = _txtTplName.Text.Trim();
        tpl.WidthMm = (float)_numTplWidth.Value;
        tpl.HeightMm = (float)_numTplHeight.Value;
        tpl.Dpi = (int)_numTplDpi.Value;
        tpl.FontFamily = _cboTplFont.SelectedItem?.ToString() ?? "Segoe UI";
        tpl.SinhalaFontFamily = _cboTplSinhalaFont.SelectedItem?.ToString() ?? "Nirmala UI";
        tpl.TitleFontSize = (float)_numTitleFont.Value;
        tpl.BodyFontSize = (float)_numBodyFont.Value;

        tpl.ShowSinhalaName = _chkSinhala.Checked;
        tpl.ShowManufactureDate = _chkMDate.Checked;
        tpl.ShowExpiryDate = _chkExpDate.Checked;
        tpl.ShowBatchNo = _chkBatch.Checked;
        tpl.ShowPrice = _chkPrice.Checked;
        tpl.ShowWeight = _chkWeight.Checked;
        tpl.ShowDescription = _chkDesc.Checked;
        tpl.ShowTel = _chkPhone.Checked;
        tpl.ShowRegistration = _chkReg.Checked;
        tpl.ShowBarcode = _chkBarcode.Checked;

        return tpl;
    }

    private void SaveCurrentTemplateFromEditor()
    {
        var tpl = BuildTemplateFromEditor();
        if (string.IsNullOrWhiteSpace(tpl.Name))
        {
            MessageBox.Show("Please enter a template name.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _store.AddOrUpdateTemplate(tpl);
        RefreshTemplatesList();
        PopulateStudioTemplates();
        MessageBox.Show($"Template '{tpl.Name}' saved successfully!", "Template Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void CreateNewTemplate()
    {
        var newTpl = new LabelTemplate
        {
            Id = Guid.NewGuid().ToString(),
            Name = $"Custom Label {_store.Templates.Count + 1}",
            WidthMm = 50f,
            HeightMm = 40f,
            Dpi = 203,
            IsBuiltIn = false
        };
        _store.AddOrUpdateTemplate(newTpl);
        RefreshTemplatesList();
        PopulateStudioTemplates();

        for (int i = 0; i < _templateList.Items.Count; i++)
        {
            if (_templateList.Items[i].Tag is LabelTemplate t && t.Id == newTpl.Id)
            {
                _templateList.Items[i].Selected = true;
                break;
            }
        }
    }

    private void DeleteCurrentTemplate()
    {
        if (_templateList.SelectedItems.Count > 0 && _templateList.SelectedItems[0].Tag is LabelTemplate tpl)
        {
            if (tpl.IsBuiltIn)
            {
                MessageBox.Show("Built-in standard templates cannot be deleted.", "Protected Template", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (MessageBox.Show($"Delete template '{tpl.Name}'?", "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                _store.DeleteTemplate(tpl.Id);
                RefreshTemplatesList();
                PopulateStudioTemplates();
            }
        }
    }

    #endregion

    #region Settings View

    private void BuildSettingsView()
    {
        _settingsView = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };

        var card = new ModernCard { Dock = DockStyle.Fill, CornerRadius = 14, Padding = new Padding(24) };
        var lblTitle = new Label { Text = "⚙️ Hardware & Application Settings", Font = new Font("Segoe UI", 12f, FontStyle.Bold), Location = new Point(16, 16), AutoSize = true };

        int y = 54;

        AddFormLabel(card, "Printer Connection Type", 16, ref y);
        var pnlConnType = new FlowLayoutPanel
        {
            Location = new Point(16, y),
            Width = 420,
            Height = 32,
            FlowDirection = FlowDirection.LeftToRight
        };
        _rbConnWindows = new RadioButton
        {
            Text = "🖨️ Windows Installed Driver",
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Checked = _store.Settings.ConnectionType == PrinterConnectionType.WindowsDriver,
            Margin = new Padding(0, 0, 16, 0)
        };
        _rbConnBluetooth = new RadioButton
        {
            Text = "📶 Bluetooth / Serial (COM Port)",
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Checked = _store.Settings.ConnectionType == PrinterConnectionType.BluetoothSerial
        };
        pnlConnType.Controls.AddRange([_rbConnWindows, _rbConnBluetooth]);
        card.Controls.Add(pnlConnType);
        y += 34;

        var pnlPrinterHolder = new Panel
        {
            Location = new Point(16, y),
            Width = 420,
            Height = 150,
            BackColor = Color.Transparent
        };

        // Panel 1: Windows Printer
        _pnlWindowsPrinter = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent
        };
        int wy = 0;
        AddFormLabel(_pnlWindowsPrinter, "Active Windows Printer", 0, ref wy);
        _cboSettingsPrinter = new ComboBox { Location = new Point(0, wy), Width = 400, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 10f) };
        _pnlWindowsPrinter.Controls.Add(_cboSettingsPrinter); wy += 36;

        AddFormLabel(_pnlWindowsPrinter, "Print Engine / Communication Mode", 0, ref wy);
        _cboSettingsEngine = new ComboBox { Location = new Point(0, wy), Width = 400, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 10f) };
        _cboSettingsEngine.Items.AddRange(["Windows Driver (Recommended, High Precision Unicode GDI)", "TSPL Direct Raw (Thermal Label Printers)", "ESC/POS Raster (POS Receipt Printers)"]);
        _cboSettingsEngine.SelectedIndex = 0;
        _pnlWindowsPrinter.Controls.Add(_cboSettingsEngine);

        // Panel 2: Bluetooth Printer
        _pnlBluetoothPrinter = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent
        };
        int by = 0;
        AddFormLabel(_pnlBluetoothPrinter, "Active Bluetooth / COM Port", 0, ref by);
        _cboBluetoothPort = new ComboBox { Location = new Point(0, by), Width = 400, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9.5f) };
        _pnlBluetoothPrinter.Controls.Add(_cboBluetoothPort); by += 32;

        var pnlBtButtons = new FlowLayoutPanel
        {
            Location = new Point(0, by),
            Width = 400,
            Height = 32,
            FlowDirection = FlowDirection.LeftToRight
        };
        var btnRefreshBt = new Button
        {
            Text = "🔄 Refresh Ports",
            Size = new Size(110, 28),
            Font = new Font("Segoe UI", 8.5f),
            FlatStyle = FlatStyle.System
        };
        btnRefreshBt.Click += (s, e) => LoadBluetoothPorts();

        var btnPairBt = new Button
        {
            Text = "📶 Windows Bluetooth",
            Size = new Size(140, 28),
            Font = new Font("Segoe UI", 8.5f),
            FlatStyle = FlatStyle.System
        };
        btnPairBt.Click += (s, e) => BluetoothManager.OpenBluetoothSettings();

        var lblBaud = new Label { Text = "Baud:", AutoSize = true, Margin = new Padding(6, 6, 2, 0), Font = new Font("Segoe UI", 8.5f) };
        _cboBluetoothBaud = new ComboBox { Width = 80, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 8.5f) };
        _cboBluetoothBaud.Items.AddRange(["9600", "115200", "38400", "19200"]);
        _cboBluetoothBaud.SelectedItem = _store.Settings.BluetoothBaudRate > 0 ? _store.Settings.BluetoothBaudRate.ToString() : "9600";
        if (_cboBluetoothBaud.SelectedIndex < 0) _cboBluetoothBaud.SelectedIndex = 0;

        pnlBtButtons.Controls.AddRange([btnRefreshBt, btnPairBt, lblBaud, _cboBluetoothBaud]);
        _pnlBluetoothPrinter.Controls.Add(pnlBtButtons); by += 34;

        AddFormLabel(_pnlBluetoothPrinter, "Protocol / Command Set", 0, ref by);
        _cboBluetoothProtocol = new ComboBox { Location = new Point(0, by), Width = 400, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9.5f) };
        _cboBluetoothProtocol.Items.AddRange(["TSPL Direct Raw (Thermal Label Printers - Xprinter, 4Barcode)", "ESC/POS Raster (Receipt & Mini Mobile Printers - 58mm/80mm)"]);
        _cboBluetoothProtocol.SelectedIndex = _store.Settings.PrintEngine == PrintEngine.EscPosRaster ? 1 : 0;
        _pnlBluetoothPrinter.Controls.Add(_cboBluetoothProtocol);

        pnlPrinterHolder.Controls.Add(_pnlWindowsPrinter);
        pnlPrinterHolder.Controls.Add(_pnlBluetoothPrinter);
        card.Controls.Add(pnlPrinterHolder);

        void UpdateConnTypeView()
        {
            bool isBt = _rbConnBluetooth.Checked;
            _pnlWindowsPrinter.Visible = !isBt;
            _pnlBluetoothPrinter.Visible = isBt;
        }
        _rbConnWindows.CheckedChanged += (s, e) => UpdateConnTypeView();
        _rbConnBluetooth.CheckedChanged += (s, e) => UpdateConnTypeView();
        UpdateConnTypeView();

        y += 154;

        AddFormLabel(card, "Default Company / Brand Name", 16, ref y);
        _txtSettingsCompany = AddFormTextBox(card, 16, ref y, 400);
        _txtSettingsCompany.Text = _store.Settings.CompanyName;

        AddFormLabel(card, "Default Contact Telephone", 16, ref y);
        _txtSettingsPhone = AddFormTextBox(card, 16, ref y, 400);
        _txtSettingsPhone.Text = _store.Settings.CompanyPhone;

        AddFormLabel(card, "Currency Symbol (e.g. /= or Rs.)", 16, ref y);
        _txtSettingsCurrency = AddFormTextBox(card, 16, ref y, 400);
        _txtSettingsCurrency.Text = _store.Settings.CurrencySymbol;

        var btnTestPrint = new ModernButton
        {
            Text = "🖨️ Test Print Label",
            DefaultBg = Color.FromArgb(241, 245, 249),
            HoverBg = Color.FromArgb(226, 232, 240),
            ForeColor = ModernColors.TextDark,
            Width = 160,
            Height = 38,
            Location = new Point(16, y + 10)
        };
        btnTestPrint.Click += (s, e) => ExecuteTestPrint();

        var btnSave = new ModernButton
        {
            Text = "💾 Save Settings",
            DefaultBg = ModernColors.Primary,
            HoverBg = ModernColors.PrimaryHover,
            Width = 160,
            Height = 38,
            Location = new Point(190, y + 10)
        };
        btnSave.Click += (s, e) => SaveSettingsFromForm();

        // Right Column: Software Updates
        int uy = 54;
        var lblUpdateSection = new Label
        {
            Text = "🔄 LunaPOS Cloud Updates",
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            ForeColor = ModernColors.TextDark,
            Location = new Point(460, uy),
            AutoSize = true
        };
        card.Controls.Add(lblUpdateSection);
        uy += 30;

        var lblCurrentVer = new Label
        {
            Text = $"Installed Version: v{AppConfig.CurrentVersion} (Official Production)",
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = ModernColors.TextDark,
            Location = new Point(460, uy),
            AutoSize = true
        };
        card.Controls.Add(lblCurrentVer);
        uy += 26;

        _chkAutoUpdate = new CheckBox
        {
            Text = "Automatically check for updates on startup",
            Checked = _store.Settings.AutoCheckUpdates,
            Font = new Font("Segoe UI", 9f),
            ForeColor = ModernColors.TextDark,
            Location = new Point(460, uy),
            AutoSize = true
        };
        card.Controls.Add(_chkAutoUpdate);
        uy += 32;

        _btnCheckUpdates = new ModernButton
        {
            Text = "🔄 Check for Updates Now",
            DefaultBg = ModernColors.Primary,
            HoverBg = ModernColors.PrimaryHover,
            Width = 210,
            Height = 36,
            Location = new Point(460, uy)
        };
        _btnCheckUpdates.Click += async (s, e) => await CheckForUpdatesManualAsync();
        card.Controls.Add(_btnCheckUpdates);
        uy += 44;

        _lblUpdateStatus = new Label
        {
            Text = "System is ready to check for official updates.",
            Font = new Font("Segoe UI", 9f),
            ForeColor = ModernColors.TextMuted,
            Location = new Point(460, uy),
            Width = 380,
            Height = 44
        };
        card.Controls.Add(_lblUpdateStatus);

        card.Controls.AddRange([lblTitle, btnTestPrint, btnSave]);
        _settingsView.Controls.Add(card);
    }

    private async Task CheckForUpdatesManualAsync()
    {
        _btnCheckUpdates.Enabled = false;
        _btnCheckUpdates.Text = "Checking...";
        _lblUpdateStatus.Text = "Connecting to LunaPOS update service...";
        _lblUpdateStatus.ForeColor = ModernColors.TextMuted;

        var res = await UpdateManager.CheckForUpdatesAsync();

        _btnCheckUpdates.Enabled = true;
        _btnCheckUpdates.Text = "🔄 Check for Updates Now";

        if (!string.IsNullOrEmpty(res.ErrorMessage))
        {
            _lblUpdateStatus.Text = res.ErrorMessage;
            _lblUpdateStatus.ForeColor = ModernColors.Danger;
            return;
        }

        if (res.HasUpdate)
        {
            _lblUpdateStatus.Text = $"✨ New version v{res.LatestVersion} is available!";
            _lblUpdateStatus.ForeColor = ModernColors.Primary;

            var dr = MessageBox.Show(
                $"A new official update is available!\n\n" +
                $"Current Version: v{res.CurrentVersion}\n" +
                $"Latest Version: v{res.LatestVersion}\n\n" +
                (string.IsNullOrWhiteSpace(res.Changelog) ? "" : $"What's New:\n{res.Changelog}\n\n") +
                "Would you like to download and install this update now?",
                "LunaPOS Software Update",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information);

            if (dr == DialogResult.Yes)
            {
                using var dlg = new UpdateProgressDialog(res);
                dlg.ShowDialog(this);
            }
        }
        else
        {
            _lblUpdateStatus.Text = $"✓ LunaPOS Label Studio is up to date (v{res.CurrentVersion}).";
            _lblUpdateStatus.ForeColor = ModernColors.Success;
            MessageBox.Show($"You are using the latest version of LunaPOS Label Studio (v{res.CurrentVersion}).", "LunaPOS Update", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void ShowUpdateBanner(UpdateCheckResult res)
    {
        if (_pnlUpdateBanner != null || IsDisposed) return;

        _pnlUpdateBanner = new Panel
        {
            Dock = DockStyle.Top,
            Height = 42,
            BackColor = Color.FromArgb(238, 242, 255),
            Padding = new Padding(16, 6, 16, 6)
        };
        _pnlUpdateBanner.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(199, 210, 254), 1.2f);
            e.Graphics.DrawRectangle(pen, 0, 0, _pnlUpdateBanner.Width - 1, _pnlUpdateBanner.Height - 1);
        };

        var lblIcon = new Label { Text = "✨", Font = new Font("Segoe UI", 12f), Location = new Point(12, 10), AutoSize = true };
        var lblMsg = new Label
        {
            Text = $"A new version of LunaPOS Label Studio (v{res.LatestVersion}) is available!",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = ModernColors.Primary,
            Location = new Point(42, 11),
            AutoSize = true
        };

        var btnUpdateNow = new ModernButton
        {
            Text = "🚀 Update Now",
            DefaultBg = ModernColors.Primary,
            HoverBg = ModernColors.PrimaryHover,
            Width = 115,
            Height = 28,
            Location = new Point(lblMsg.Right + 20, 7)
        };
        btnUpdateNow.Click += (s, e) =>
        {
            using var dlg = new UpdateProgressDialog(res);
            dlg.ShowDialog(this);
        };

        var btnDismiss = new Button
        {
            Text = "✕",
            FlatStyle = FlatStyle.Flat,
            ForeColor = ModernColors.TextMuted,
            Size = new Size(24, 24),
            Dock = DockStyle.Right,
            Cursor = Cursors.Hand
        };
        btnDismiss.FlatAppearance.BorderSize = 0;
        btnDismiss.Click += (s, e) =>
        {
            _contentContainer.Controls.Remove(_pnlUpdateBanner);
            _pnlUpdateBanner.Dispose();
            _pnlUpdateBanner = null;
        };

        _pnlUpdateBanner.Controls.AddRange([lblIcon, lblMsg, btnUpdateNow, btnDismiss]);
        _contentContainer.Controls.Add(_pnlUpdateBanner);
        _pnlUpdateBanner.BringToFront();
    }

    private void UpdateHeaderPrinterBadge()
    {
        if (_lblHeaderPrinter == null) return;
        if (_store.Settings.ConnectionType == PrinterConnectionType.BluetoothSerial)
        {
            string port = string.IsNullOrEmpty(_store.Settings.BluetoothPort) ? "Not Selected" : _store.Settings.BluetoothPort;
            _lblHeaderPrinter.Text = $"📶 Bluetooth: {port}";
        }
        else
        {
            string p = string.IsNullOrEmpty(_store.Settings.SelectedPrinter) ? "Not Selected" : _store.Settings.SelectedPrinter;
            _lblHeaderPrinter.Text = $"🖨️ Printer: {p}";
        }
    }

    private void LoadBluetoothPorts()
    {
        if (_cboBluetoothPort == null) return;
        _cboBluetoothPort.Items.Clear();

        var ports = BluetoothManager.GetAvailablePorts();
        foreach (var p in ports)
        {
            _cboBluetoothPort.Items.Add(p);
        }

        if (!string.IsNullOrEmpty(_store.Settings.BluetoothPort))
        {
            for (int i = 0; i < _cboBluetoothPort.Items.Count; i++)
            {
                if (_cboBluetoothPort.Items[i] is BluetoothPortInfo info &&
                    string.Equals(info.PortName, _store.Settings.BluetoothPort, StringComparison.OrdinalIgnoreCase))
                {
                    _cboBluetoothPort.SelectedIndex = i;
                    break;
                }
            }
        }

        if (_cboBluetoothPort.SelectedIndex < 0 && _cboBluetoothPort.Items.Count > 0)
        {
            _cboBluetoothPort.SelectedIndex = 0;
        }

        if (_cboBluetoothPort.SelectedItem is BluetoothPortInfo sel)
        {
            _store.Settings.BluetoothPort = sel.PortName;
        }

        UpdateHeaderPrinterBadge();
    }

    private void LoadPrinters()
    {
        _cboSettingsPrinter.Items.Clear();
        foreach (string printer in PrinterSettings.InstalledPrinters)
        {
            _cboSettingsPrinter.Items.Add(printer);
        }

        // Auto select preferred printer
        if (!string.IsNullOrEmpty(_store.Settings.SelectedPrinter) && _cboSettingsPrinter.Items.Contains(_store.Settings.SelectedPrinter))
        {
            _cboSettingsPrinter.SelectedItem = _store.Settings.SelectedPrinter;
        }
        else
        {
            // Auto find barcode printer
            int selectedIndex = 0;
            for (int i = 0; i < _cboSettingsPrinter.Items.Count; i++)
            {
                string name = _cboSettingsPrinter.Items[i].ToString() ?? "";
                if (name.Contains("4BARCODE", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("365B", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("XP-", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Label", StringComparison.OrdinalIgnoreCase))
                {
                    selectedIndex = i;
                    break;
                }
            }
            if (_cboSettingsPrinter.Items.Count > 0)
                _cboSettingsPrinter.SelectedIndex = selectedIndex;
        }

        if (_cboSettingsPrinter.SelectedItem != null)
        {
            _store.Settings.SelectedPrinter = _cboSettingsPrinter.SelectedItem.ToString() ?? "";
        }

        _cboSettingsEngine.SelectedIndex = (int)_store.Settings.PrintEngine;

        LoadBluetoothPorts();
        UpdateHeaderPrinterBadge();
    }

    private void SaveSettingsFromForm()
    {
        _store.Settings.ConnectionType = _rbConnBluetooth.Checked
            ? PrinterConnectionType.BluetoothSerial
            : PrinterConnectionType.WindowsDriver;

        _store.Settings.SelectedPrinter = _cboSettingsPrinter.SelectedItem?.ToString() ?? "";

        if (_cboBluetoothPort.SelectedItem is BluetoothPortInfo btInfo)
        {
            _store.Settings.BluetoothPort = btInfo.PortName;
        }
        else if (_cboBluetoothPort.SelectedItem != null)
        {
            _store.Settings.BluetoothPort = _cboBluetoothPort.SelectedItem.ToString() ?? "";
        }

        if (_cboBluetoothBaud.SelectedItem != null && int.TryParse(_cboBluetoothBaud.SelectedItem.ToString(), out int baud))
        {
            _store.Settings.BluetoothBaudRate = baud;
        }

        if (_store.Settings.ConnectionType == PrinterConnectionType.BluetoothSerial)
        {
            _store.Settings.PrintEngine = _cboBluetoothProtocol.SelectedIndex == 1
                ? PrintEngine.EscPosRaster
                : PrintEngine.TsplRaw;
        }
        else
        {
            _store.Settings.PrintEngine = (PrintEngine)_cboSettingsEngine.SelectedIndex;
        }

        _store.Settings.CompanyName = _txtSettingsCompany.Text.Trim();
        _store.Settings.CompanyPhone = _txtSettingsPhone.Text.Trim();
        _store.Settings.CurrencySymbol = _txtSettingsCurrency.Text.Trim();
        if (_chkAutoUpdate != null)
        {
            _store.Settings.AutoCheckUpdates = _chkAutoUpdate.Checked;
        }

        _store.SaveSettings();
        UpdateHeaderPrinterBadge();
        MessageBox.Show("Settings saved successfully!", "Settings", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ExecuteTestPrint()
    {
        SaveSettingsFromForm();
        var testProduct = _selectedProduct ?? DataStore.GetDefaultProducts()[0];
        var testJob = new PrintJob
        {
            ProductName = testProduct.Name,
            SinhalaName = testProduct.SinhalaName,
            PriceText = testProduct.DisplayPrice,
            WeightText = testProduct.Weight,
            Description = testProduct.Description,
            Tel = testProduct.Tel,
            RegistrationNo = testProduct.RegistrationNo,
            ManufactureDate = DateTime.Today,
            ExpiryDate = DateTime.Today.AddDays(30),
            BatchNumber = "TEST-01",
            Copies = 1,
            Template = _activeTemplate
        };

        try
        {
            PrintingEngine.Print(testJob, _store.Settings);
            string dest = _store.Settings.ConnectionType == PrinterConnectionType.BluetoothSerial
                ? $"Bluetooth port {_store.Settings.BluetoothPort}"
                : $"printer {_store.Settings.SelectedPrinter}";
            MessageBox.Show($"Test print sent successfully to {dest}!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Test print failed:\n{ex.Message}", "Print Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    #endregion

    #region Print History View

    private void BuildHistoryView()
    {
        _historyView = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };

        var card = new ModernCard { Dock = DockStyle.Fill, CornerRadius = 14, Padding = new Padding(16) };
        var lblTitle = new Label { Text = "📜 Print Job History & Audit Log", Font = new Font("Segoe UI", 11.5f, FontStyle.Bold), Location = new Point(14, 12), AutoSize = true };

        _historyTable = new ListView
        {
            Location = new Point(14, 46),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            Width = card.Width - 28,
            Height = card.Height - 60,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            BorderStyle = BorderStyle.None,
            Font = new Font("Segoe UI", 9.5f)
        };
        _historyTable.Columns.Add("Date & Time", 160);
        _historyTable.Columns.Add("Product", 200);
        _historyTable.Columns.Add("Sinhala Name", 160);
        _historyTable.Columns.Add("Batch No", 100);
        _historyTable.Columns.Add("Price", 90);
        _historyTable.Columns.Add("Copies", 80);
        _historyTable.Columns.Add("Template", 140);
        _historyTable.Columns.Add("Printer Used", 180);

        card.Controls.AddRange([lblTitle, _historyTable]);
        _historyView.Controls.Add(card);
    }

    private void RefreshHistoryTable()
    {
        _historyTable.Items.Clear();
        foreach (var h in _store.History)
        {
            var item = new ListViewItem(h.Timestamp.ToString("yyyy/MM/dd HH:mm:ss"));
            item.SubItems.Add(h.ProductName);
            item.SubItems.Add(h.SinhalaName);
            item.SubItems.Add(h.BatchNo);
            item.SubItems.Add(h.Price);
            item.SubItems.Add(h.Copies.ToString());
            item.SubItems.Add(h.TemplateName);
            item.SubItems.Add(h.PrinterName);
            _historyTable.Items.Add(item);
        }
    }

    #endregion

    #region Helper UI Builders

    private static void AddFormLabel(Control parent, string text, int x, ref int y)
    {
        var lbl = new Label
        {
            Text = text,
            Location = new Point(x, y),
            AutoSize = true,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = ModernColors.TextDark
        };
        parent.Controls.Add(lbl);
        y += 18;
    }

    private static TextBox AddFormTextBox(Control parent, int x, ref int y, int width)
    {
        var tb = new TextBox
        {
            Location = new Point(x, y),
            Width = width,
            Font = new Font("Segoe UI", 9.5f)
        };
        parent.Controls.Add(tb);
        y += 32;
        return tb;
    }

    #endregion
}
