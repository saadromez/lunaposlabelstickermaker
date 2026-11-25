using System.Text.Json;

namespace LunaPosLabelStickerMaker;

public class DataStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _storageDir;
    private readonly string _productsFile;
    private readonly string _templatesFile;
    private readonly string _settingsFile;
    private readonly string _historyFile;

    public List<ProductItem> Products { get; private set; } = [];
    public List<LabelTemplate> Templates { get; private set; } = [];
    public AppSettings Settings { get; private set; } = new();
    public List<PrintHistoryItem> History { get; private set; } = [];

    public DataStore()
    {
        _storageDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LunaPosLabelStickerMaker");

        Directory.CreateDirectory(_storageDir);

        _productsFile = Path.Combine(_storageDir, "products.json");
        _templatesFile = Path.Combine(_storageDir, "templates.json");
        _settingsFile = Path.Combine(_storageDir, "settings.json");
        _historyFile = Path.Combine(_storageDir, "history.json");

        LoadAll();
    }

    public void LoadAll()
    {
        LoadSettings();
        LoadTemplates();
        LoadProducts();
        LoadHistory();
    }

    private void LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsFile))
            {
                var json = File.ReadAllText(_settingsFile);
                Settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            }
            else
            {
                Settings = new AppSettings();
                SaveSettings();
            }
        }
        catch
        {
            Settings = new AppSettings();
        }
    }

    public void SaveSettings()
    {
        try
        {
            var json = JsonSerializer.Serialize(Settings, JsonOptions);
            File.WriteAllText(_settingsFile, json);
        }
        catch { }
    }

    private void LoadTemplates()
    {
        try
        {
            if (File.Exists(_templatesFile))
            {
                var json = File.ReadAllText(_templatesFile);
                Templates = JsonSerializer.Deserialize<List<LabelTemplate>>(json, JsonOptions) ?? [];
            }
        }
        catch
        {
            Templates = [];
        }

        if (Templates.Count == 0)
        {
            Templates = GetDefaultTemplates();
            SaveTemplates();
        }
        else
        {
            // Ensure built-in standard 40x45 (4x4.5cm) template exists
            if (!Templates.Any(t => t.Id == "tpl-bakery-40x45"))
            {
                Templates.Insert(0, GetDefaultTemplates()[0]);
                SaveTemplates();
            }
            foreach (var t in Templates)
            {
                if (t.LayoutReg != null && t.LayoutReg.RotationDeg == 0)
                {
                    t.LayoutReg.RotationDeg = 90;
                }
            }
        }
    }

    public void SaveTemplates()
    {
        try
        {
            var json = JsonSerializer.Serialize(Templates, JsonOptions);
            File.WriteAllText(_templatesFile, json);
        }
        catch { }
    }

    private void LoadProducts()
    {
        try
        {
            if (File.Exists(_productsFile))
            {
                var json = File.ReadAllText(_productsFile);
                Products = JsonSerializer.Deserialize<List<ProductItem>>(json, JsonOptions) ?? [];
            }
        }
        catch
        {
            Products = [];
        }

        if (Products.Count == 0)
        {
            Products = GetDefaultProducts();
            SaveProducts();
        }
    }

    public void SaveProducts()
    {
        try
        {
            var json = JsonSerializer.Serialize(Products, JsonOptions);
            File.WriteAllText(_productsFile, json);
        }
        catch { }
    }

    private void LoadHistory()
    {
        try
        {
            if (File.Exists(_historyFile))
            {
                var json = File.ReadAllText(_historyFile);
                History = JsonSerializer.Deserialize<List<PrintHistoryItem>>(json, JsonOptions) ?? [];
            }
        }
        catch
        {
            History = [];
        }
    }

    public void AddHistory(PrintJob job, string printerName)
    {
        var item = new PrintHistoryItem
        {
            ProductName = job.ProductName,
            SinhalaName = job.SinhalaName,
            BatchNo = job.BatchNumber,
            Price = job.PriceText,
            Weight = job.WeightText,
            Copies = job.Copies,
            PrinterName = printerName,
            TemplateName = job.Template?.Name ?? "Standard",
            Timestamp = DateTime.Now
        };

        History.Insert(0, item);
        if (History.Count > 200) History.RemoveAt(History.Count - 1);

        try
        {
            var json = JsonSerializer.Serialize(History, JsonOptions);
            File.WriteAllText(_historyFile, json);
        }
        catch { }
    }

    public void AddOrUpdateProduct(ProductItem product)
    {
        int idx = Products.FindIndex(p => p.Id == product.Id);
        if (idx >= 0)
            Products[idx] = product;
        else
            Products.Add(product);

        SaveProducts();
    }

    public void DeleteProduct(string id)
    {
        Products.RemoveAll(p => p.Id == id);
        SaveProducts();
    }

    public void AddOrUpdateTemplate(LabelTemplate tpl)
    {
        int idx = Templates.FindIndex(t => t.Id == tpl.Id);
        if (idx >= 0)
            Templates[idx] = tpl;
        else
            Templates.Add(tpl);

        SaveTemplates();
    }

    public void DeleteTemplate(string id)
    {
        Templates.RemoveAll(t => t.Id == id && !t.IsBuiltIn);
        SaveTemplates();
    }

    public static List<LabelTemplate> GetDefaultTemplates()
    {
        return
        [
            new LabelTemplate
            {
                Id = "tpl-bakery-40x45",
                Name = "Bakery Label (40x45mm) - Reference 4×4.5cm",
                WidthMm = 40f,
                HeightMm = 45f,
                Dpi = 203,
                LayoutType = LabelLayoutType.FoodBakery,
                ShowSinhalaName = true,
                ShowManufactureDate = true,
                ShowExpiryDate = true,
                ShowBatchNo = true,
                ShowPrice = true,
                ShowWeight = true,
                ShowDescription = true,
                ShowTel = true,
                ShowRegistration = true,
                ShowBarcode = false,
                BorderRounded = true,
                BorderRadiusMm = 2.0f,
                TitleFontSize = 9.5f,
                BodyFontSize = 6.2f,
                FontFamily = "Segoe UI",
                SinhalaFontFamily = "Nirmala UI",
                IsBuiltIn = true
            },
            new LabelTemplate
            {
                Id = "tpl-bakery-50x40",
                Name = "Bakery Food Label (50x40mm)",
                WidthMm = 50f,
                HeightMm = 40f,
                Dpi = 203,
                LayoutType = LabelLayoutType.FoodBakery,
                ShowSinhalaName = true,
                ShowManufactureDate = true,
                ShowExpiryDate = true,
                ShowBatchNo = true,
                ShowPrice = true,
                ShowWeight = true,
                ShowDescription = true,
                ShowTel = true,
                ShowRegistration = true,
                ShowBarcode = false,
                BorderRounded = true,
                BorderRadiusMm = 2.0f,
                TitleFontSize = 10.5f,
                BodyFontSize = 7.0f,
                FontFamily = "Segoe UI",
                SinhalaFontFamily = "Nirmala UI",
                IsBuiltIn = true
            },
            new LabelTemplate
            {
                Id = "tpl-bakery-50x50",
                Name = "Square Food Label (50x50mm)",
                WidthMm = 50f,
                HeightMm = 50f,
                Dpi = 203,
                LayoutType = LabelLayoutType.FoodBakery,
                ShowSinhalaName = true,
                ShowManufactureDate = true,
                ShowExpiryDate = true,
                ShowBatchNo = true,
                ShowPrice = true,
                ShowWeight = true,
                ShowDescription = true,
                ShowTel = true,
                ShowRegistration = true,
                ShowBarcode = true,
                BorderRounded = true,
                BorderRadiusMm = 2.0f,
                TitleFontSize = 11f,
                BodyFontSize = 7.5f,
                FontFamily = "Nirmala UI",
                IsBuiltIn = true
            },
            new LabelTemplate
            {
                Id = "tpl-retail-40x30",
                Name = "Retail Price & Barcode (40x30mm)",
                WidthMm = 40f,
                HeightMm = 30f,
                Dpi = 203,
                LayoutType = LabelLayoutType.RetailBarcode,
                ShowSinhalaName = true,
                ShowManufactureDate = true,
                ShowExpiryDate = true,
                ShowBatchNo = false,
                ShowPrice = true,
                ShowWeight = false,
                ShowDescription = false,
                ShowTel = false,
                ShowRegistration = false,
                ShowBarcode = true,
                BorderRounded = true,
                BorderRadiusMm = 1.5f,
                TitleFontSize = 9.0f,
                BodyFontSize = 7.0f,
                FontFamily = "Segoe UI",
                IsBuiltIn = true
            },
            new LabelTemplate
            {
                Id = "tpl-compact-30x20",
                Name = "Compact Barcode Sticker (30x20mm)",
                WidthMm = 30f,
                HeightMm = 20f,
                Dpi = 203,
                LayoutType = LabelLayoutType.RetailBarcode,
                ShowSinhalaName = false,
                ShowManufactureDate = false,
                ShowExpiryDate = false,
                ShowBatchNo = false,
                ShowPrice = true,
                ShowWeight = false,
                ShowDescription = false,
                ShowTel = false,
                ShowRegistration = false,
                ShowBarcode = true,
                BorderRounded = false,
                TitleFontSize = 7.5f,
                BodyFontSize = 6.0f,
                FontFamily = "Arial",
                IsBuiltIn = true
            }
        ];
    }

    public static List<ProductItem> GetDefaultProducts()
    {
        return
        [
            new ProductItem
            {
                Id = "prod-001",
                Name = "Suger Biscuit",
                SinhalaName = "සීනි බිස්කට්",
                Category = "Bakery & Biscuits",
                DefaultPrice = 260,
                PriceFormat = "{0}/=",
                Weight = "360-380g",
                ShelfLifeDays = 30,
                BatchPrefix = "11",
                Barcode = "112100260",
                Sku = "SB-260",
                Description = "තුනී තැටි රෝල් කෙටි කෑමක් රසැති නිශ්පාදන කිසිදු කල්තබා ගැනීමේ රසායනික ද්‍රව්‍ය අඩංගු නැත.",
                RegistrationNo = "දි:ව ලියාපදිංචි -    2/1/85/2022-02-25/17",
                Tel = "Tel-070-2220111",
                PreferredTemplateId = "tpl-bakery-50x40"
            },
            new ProductItem
            {
                Id = "prod-002",
                Name = "Butter Cake",
                SinhalaName = "බටර් කේක්",
                Category = "Cakes & Pastries",
                DefaultPrice = 650,
                PriceFormat = "{0}/=",
                Weight = "450g",
                ShelfLifeDays = 14,
                BatchPrefix = "BK",
                Barcode = "202400650",
                Sku = "BC-650",
                Description = "උසස් තත්ත්වයේ බටර් සහ නැවුම් බිත්තර යොදා සකස් කරන ලද රසවත් බටර් කේක්.",
                RegistrationNo = "දි:ව ලියාපදිංචි -    2/1/85/2022-02-25/17",
                Tel = "Tel-070-2220111",
                PreferredTemplateId = "tpl-bakery-50x40"
            },
            new ProductItem
            {
                Id = "prod-003",
                Name = "Kithul Hakuru",
                SinhalaName = "කිතුල් හකුරු",
                Category = "Traditional Sweets",
                DefaultPrice = 480,
                PriceFormat = "{0}/=",
                Weight = "350g",
                ShelfLifeDays = 90,
                BatchPrefix = "KH",
                Barcode = "303100480",
                Sku = "KH-480",
                Description = "100% ස්වභාවික පිරිසිදු කිතුල් පැණියෙන් සාදන ලද ගුණදායක කිතුල් හකුරු.",
                RegistrationNo = "දි:ව ලියාපදිංචි -    2/1/85/2022-02-25/17",
                Tel = "Tel-070-2220111",
                PreferredTemplateId = "tpl-bakery-50x40"
            },
            new ProductItem
            {
                Id = "prod-004",
                Name = "Spicy Mixture",
                SinhalaName = "සැර මික්ස්චර්",
                Category = "Snacks & Bites",
                DefaultPrice = 180,
                PriceFormat = "{0}/=",
                Weight = "150g",
                ShelfLifeDays = 60,
                BatchPrefix = "MX",
                Barcode = "404100180",
                Sku = "MX-180",
                Description = "නැවුම් කුළුබඩු මිශ්‍ර රසවත් සහ කර කර ගා හැපෙන උසස් තත්ත්වයේ මික්ස්චර්.",
                RegistrationNo = "දි:ව ලියාපදිංචි -    2/1/85/2022-02-25/17",
                Tel = "Tel-070-2220111",
                PreferredTemplateId = "tpl-bakery-50x40"
            },
            new ProductItem
            {
                Id = "prod-005",
                Name = "Chocolate Roll",
                SinhalaName = "චොකලට් රෝල්",
                Category = "Bakery & Biscuits",
                DefaultPrice = 320,
                PriceFormat = "{0}/=",
                Weight = "250g",
                ShelfLifeDays = 21,
                BatchPrefix = "CR",
                Barcode = "505100320",
                Sku = "CR-320",
                Description = "පිරිසිදු චොකලට් ක්‍රීම් පිරවූ ප්‍රණීත සහ මෘදු රෝල් කේක්.",
                RegistrationNo = "දි:ව ලියාපදිංචි -    2/1/85/2022-02-25/17",
                Tel = "Tel-070-2220111",
                PreferredTemplateId = "tpl-bakery-50x40"
            },
            new ProductItem
            {
                Id = "prod-006",
                Name = "Milk Toffee",
                SinhalaName = "මිල්ක් ටොෆි",
                Category = "Traditional Sweets",
                DefaultPrice = 220,
                PriceFormat = "{0}/=",
                Weight = "200g",
                ShelfLifeDays = 45,
                BatchPrefix = "MT",
                Barcode = "606100220",
                Sku = "MT-220",
                Description = "නැවුම් එළකිරි සහ කජු මුසු කර නිපදවූ සුවිශේෂී ගෙදර හැදූ රසයෙන් යුත් මිල්ක් ටොෆි.",
                RegistrationNo = "දි:ව ලියාපදිංචි -    2/1/85/2022-02-25/17",
                Tel = "Tel-070-2220111",
                PreferredTemplateId = "tpl-bakery-50x40"
            }
        ];
    }
}
