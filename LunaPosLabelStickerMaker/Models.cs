using System.Text.Json.Serialization;

namespace LunaPosLabelStickerMaker;

public enum PrintEngine
{
    WindowsDriver,
    TsplRaw,
    EscPosRaster
}

public enum LabelLayoutType
{
    FoodBakery,
    RetailBarcode,
    MinimalDatePrice,
    Custom
}

public class ProductItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("sinhala_name")]
    public string SinhalaName { get; set; } = "";

    [JsonPropertyName("category")]
    public string Category { get; set; } = "General";

    [JsonPropertyName("default_price")]
    public double DefaultPrice { get; set; } = 0.0;

    [JsonPropertyName("price_format")]
    public string PriceFormat { get; set; } = "{0}/=";

    [JsonPropertyName("weight")]
    public string Weight { get; set; } = "";

    [JsonPropertyName("shelf_life_days")]
    public int ShelfLifeDays { get; set; } = 30;

    [JsonPropertyName("batch_prefix")]
    public string BatchPrefix { get; set; } = "";

    [JsonPropertyName("barcode")]
    public string Barcode { get; set; } = "";

    [JsonPropertyName("sku")]
    public string Sku { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("registration_no")]
    public string RegistrationNo { get; set; } = "";

    [JsonPropertyName("tel")]
    public string Tel { get; set; } = "";

    [JsonPropertyName("preferred_template_id")]
    public string PreferredTemplateId { get; set; } = "";

    public string DisplayPrice
    {
        get
        {
            if (string.IsNullOrWhiteSpace(PriceFormat))
                return $"{DefaultPrice:N0}/=";
            try
            {
                if (PriceFormat.Contains("{0"))
                    return string.Format(PriceFormat, DefaultPrice);
                return $"{PriceFormat} {DefaultPrice:N0}";
            }
            catch
            {
                return $"{DefaultPrice:N0}/=";
            }
        }
    }
}

public class ElementLayout
{
    [JsonPropertyName("offset_x_mm")]
    public float OffsetXMm { get; set; } = 0f;

    [JsonPropertyName("offset_y_mm")]
    public float OffsetYMm { get; set; } = 0f;

    [JsonPropertyName("font_size")]
    public float FontSize { get; set; } = 0f; // 0 = auto-responsive based on label dimensions

    [JsonPropertyName("font_family")]
    public string FontFamily { get; set; } = "";

    [JsonPropertyName("visible")]
    public bool Visible { get; set; } = true;

    [JsonPropertyName("rotation_deg")]
    public int RotationDeg { get; set; } = 0; // 0, 90, 180, 270

    [JsonPropertyName("bold")]
    public bool? Bold { get; set; } = null; // null = natural default, true = bold, false = regular
}

public class LabelTemplate
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    public string Name { get; set; } = "Bakery Label (40x45mm) - Client Size";

    [JsonPropertyName("width_mm")]
    public float WidthMm { get; set; } = 40f;

    [JsonPropertyName("height_mm")]
    public float HeightMm { get; set; } = 45f;

    [JsonPropertyName("dpi")]
    public int Dpi { get; set; } = 203;

    [JsonPropertyName("layout_type")]
    public LabelLayoutType LayoutType { get; set; } = LabelLayoutType.FoodBakery;

    [JsonPropertyName("show_sinhala_name")]
    public bool ShowSinhalaName { get; set; } = true;

    [JsonPropertyName("show_manufacture_date")]
    public bool ShowManufactureDate { get; set; } = true;

    [JsonPropertyName("show_expiry_date")]
    public bool ShowExpiryDate { get; set; } = true;

    [JsonPropertyName("show_batch_no")]
    public bool ShowBatchNo { get; set; } = true;

    [JsonPropertyName("show_price")]
    public bool ShowPrice { get; set; } = true;

    [JsonPropertyName("show_weight")]
    public bool ShowWeight { get; set; } = true;

    [JsonPropertyName("show_description")]
    public bool ShowDescription { get; set; } = true;

    [JsonPropertyName("show_tel")]
    public bool ShowTel { get; set; } = true;

    [JsonPropertyName("show_registration")]
    public bool ShowRegistration { get; set; } = true;

    [JsonPropertyName("show_barcode")]
    public bool ShowBarcode { get; set; } = false;

    [JsonPropertyName("barcode_type")]
    public string BarcodeType { get; set; } = "Code128";

    [JsonPropertyName("font_family")]
    public string FontFamily { get; set; } = "Segoe UI";

    [JsonPropertyName("sinhala_font_family")]
    public string SinhalaFontFamily { get; set; } = "Nirmala UI";

    [JsonPropertyName("title_font_size")]
    public float TitleFontSize { get; set; } = 9.5f;

    [JsonPropertyName("body_font_size")]
    public float BodyFontSize { get; set; } = 6.2f;

    [JsonPropertyName("border_rounded")]
    public bool BorderRounded { get; set; } = true;

    [JsonPropertyName("border_radius_mm")]
    public float BorderRadiusMm { get; set; } = 2.0f;

    [JsonPropertyName("margin_mm")]
    public float MarginMm { get; set; } = 1.2f;

    [JsonPropertyName("is_built_in")]
    public bool IsBuiltIn { get; set; } = false;

    // Per-phrase custom position and font sizing
    [JsonPropertyName("layout_title")]
    public ElementLayout LayoutTitle { get; set; } = new();

    [JsonPropertyName("layout_sinhala_title")]
    public ElementLayout LayoutSinhalaTitle { get; set; } = new();

    [JsonPropertyName("layout_mdate")]
    public ElementLayout LayoutMDate { get; set; } = new();

    [JsonPropertyName("layout_expdate")]
    public ElementLayout LayoutExpDate { get; set; } = new();

    [JsonPropertyName("layout_batch")]
    public ElementLayout LayoutBatch { get; set; } = new();

    [JsonPropertyName("layout_price")]
    public ElementLayout LayoutPrice { get; set; } = new();

    [JsonPropertyName("layout_weight")]
    public ElementLayout LayoutWeight { get; set; } = new();

    [JsonPropertyName("layout_desc")]
    public ElementLayout LayoutDesc { get; set; } = new();

    [JsonPropertyName("layout_tel")]
    public ElementLayout LayoutTel { get; set; } = new();

    [JsonPropertyName("layout_reg")]
    public ElementLayout LayoutReg { get; set; } = new() { RotationDeg = 90 };
}

public class PrintJob
{
    public string ProductName { get; set; } = "";
    public string SinhalaName { get; set; } = "";
    public DateTime ManufactureDate { get; set; } = DateTime.Today;
    public DateTime ExpiryDate { get; set; } = DateTime.Today.AddDays(30);
    public string BatchNumber { get; set; } = "";
    public string PriceText { get; set; } = "";
    public string WeightText { get; set; } = "";
    public string Description { get; set; } = "";
    public string RegistrationNo { get; set; } = "";
    public string Tel { get; set; } = "";
    public string Barcode { get; set; } = "";
    public int Copies { get; set; } = 1;
    public LabelTemplate Template { get; set; } = new();
}

public class AppSettings
{
    [JsonPropertyName("selected_printer")]
    public string SelectedPrinter { get; set; } = "";

    [JsonPropertyName("print_engine")]
    public PrintEngine PrintEngine { get; set; } = PrintEngine.WindowsDriver;

    [JsonPropertyName("default_template_id")]
    public string DefaultTemplateId { get; set; } = "";

    [JsonPropertyName("default_copies")]
    public int DefaultCopies { get; set; } = 1;

    [JsonPropertyName("company_name")]
    public string CompanyName { get; set; } = "LunaPOS";

    [JsonPropertyName("company_phone")]
    public string CompanyPhone { get; set; } = "070-2220111";

    [JsonPropertyName("currency_symbol")]
    public string CurrencySymbol { get; set; } = "/=";

    [JsonPropertyName("auto_calculate_expiry")]
    public bool AutoCalculateExpiry { get; set; } = true;

    [JsonPropertyName("auto_check_updates")]
    public bool AutoCheckUpdates { get; set; } = true;
}

public class PrintHistoryItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.Now;

    [JsonPropertyName("product_name")]
    public string ProductName { get; set; } = "";

    [JsonPropertyName("sinhala_name")]
    public string SinhalaName { get; set; } = "";

    [JsonPropertyName("batch_no")]
    public string BatchNo { get; set; } = "";

    [JsonPropertyName("price")]
    public string Price { get; set; } = "";

    [JsonPropertyName("weight")]
    public string Weight { get; set; } = "";

    [JsonPropertyName("copies")]
    public int Copies { get; set; } = 1;

    [JsonPropertyName("printer_name")]
    public string PrinterName { get; set; } = "";

    [JsonPropertyName("template_name")]
    public string TemplateName { get; set; } = "";
}
