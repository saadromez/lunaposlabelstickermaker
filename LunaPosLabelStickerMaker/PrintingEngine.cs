using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Text;

namespace LunaPosLabelStickerMaker;

public static class RawPrinterHelper
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private class DOCINFOA
    {
        [MarshalAs(UnmanagedType.LPStr)] public string? pDocName;
        [MarshalAs(UnmanagedType.LPStr)] public string? pOutputFile;
        [MarshalAs(UnmanagedType.LPStr)] public string? pDataType;
    }

    [DllImport("winspool.Drv", EntryPoint = "OpenPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool OpenPrinter([MarshalAs(UnmanagedType.LPStr)] string szPrinter, out nint hPrinter, nint pd);

    [DllImport("winspool.Drv", EntryPoint = "ClosePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool ClosePrinter(nint hPrinter);

    [DllImport("winspool.Drv", EntryPoint = "StartDocPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool StartDocPrinter(nint hPrinter, int level, [In, MarshalAs(UnmanagedType.LPStruct)] DOCINFOA di);

    [DllImport("winspool.Drv", EntryPoint = "EndDocPrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool EndDocPrinter(nint hPrinter);

    [DllImport("winspool.Drv", EntryPoint = "StartPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool StartPagePrinter(nint hPrinter);

    [DllImport("winspool.Drv", EntryPoint = "EndPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool EndPagePrinter(nint hPrinter);

    [DllImport("winspool.Drv", EntryPoint = "WritePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool WritePrinter(nint hPrinter, nint pBytes, int dwCount, out int dwWritten);

    public static void SendBytes(string printerName, byte[] bytes)
    {
        if (!OpenPrinter(printerName.Normalize(), out nint hPrinter, nint.Zero))
            throw new InvalidOperationException($"Cannot open printer: '{printerName}'. (Win32 Error: {Marshal.GetLastWin32Error()})");

        try
        {
            var di = new DOCINFOA { pDocName = "LunaPOS Label", pDataType = "RAW" };
            if (!StartDocPrinter(hPrinter, 1, di))
                throw new InvalidOperationException("StartDocPrinter failed");

            StartPagePrinter(hPrinter);
            nint p = Marshal.AllocCoTaskMem(bytes.Length);
            try
            {
                Marshal.Copy(bytes, 0, p, bytes.Length);
                WritePrinter(hPrinter, p, bytes.Length, out _);
            }
            finally
            {
                Marshal.FreeCoTaskMem(p);
            }
            EndPagePrinter(hPrinter);
            EndDocPrinter(hPrinter);
        }
        finally
        {
            ClosePrinter(hPrinter);
        }
    }
}

public static class BarcodeEncoder
{
    private static readonly int[] Patterns =
    [
        212222,222122,222221,121223,121322,131222,122213,122312,132212,221213,
        221312,231212,112232,122132,122231,113222,123122,123221,224211,221122,
        223211,221132,221231,213212,223112,312131,311222,321122,321221,312212,
        322112,322211,212123,212321,232121,111323,131123,131321,113121,313121,
        211312,231112,221311,231211,213112,213211,213121,231122,213221,221312,
        221321,231121,122312,132112,132211,122321,132121,312311,321112,321211,
        312112,322112,322211,212123,212321,232121,111323,131123,131321,113121,
        313121,211312,231112,221311,231211,213112,213211,213121,231122,213221,
        221312,221321,231121,122312,132112,132211,122321,132121,312311,321112,
        321211,312112,322112,322211,212123,212321,232121,111323,131123,131321,
        113121,313121,211312,231112,221311,231211,213112,213211,213121,231122,
    ];
    private const int StartB = 104;
    private const string StopPat = "2331112";

    public static (List<(int x, int w)> bars, int totalModules) EncodeCode128(string data)
    {
        if (string.IsNullOrEmpty(data)) data = "1000";
        var values = new List<int> { StartB };
        int checksum = StartB;
        for (int i = 0; i < data.Length; i++)
        {
            int v = (data[i] >= 32 && data[i] <= 126) ? data[i] - 32 : 0;
            values.Add(v);
            checksum += v * (i + 1);
        }
        values.Add(checksum % 103);
        string patStr = string.Concat(values.Select(v => Patterns[Math.Clamp(v, 0, Patterns.Length - 1)].ToString("D6"))) + StopPat;
        var bars = new List<(int x, int w)>();
        int x = 10;
        for (int i = 0; i < patStr.Length; i++)
        {
            int w = patStr[i] - '0';
            if (i % 2 == 0) bars.Add((x, w));
            x += w;
        }
        return (bars, x + 10);
    }
}

public static class LabelRenderer
{
    public static Bitmap RenderLabel(PrintJob job, int overrideDpi = 0, bool isForPrinting = false)
    {
        var tpl = job.Template ?? new LabelTemplate();
        int dpi = overrideDpi > 0 ? overrideDpi : (tpl.Dpi > 0 ? tpl.Dpi : 203);
        float mmToPx = dpi / 25.4f;

        int widthPx = (int)Math.Round(tpl.WidthMm * mmToPx);
        int heightPx = (int)Math.Round(tpl.HeightMm * mmToPx);

        if (widthPx < 50) widthPx = 200;
        if (heightPx < 50) heightPx = 150;

        var bmp = new Bitmap(widthPx, heightPx, PixelFormat.Format32bppArgb);
        bmp.SetResolution(dpi, dpi);

        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.White);
        g.SmoothingMode = SmoothingMode.HighQuality;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        string primaryFont = !string.IsNullOrWhiteSpace(tpl.FontFamily) ? tpl.FontFamily : "Segoe UI";
        string sinhalaFont = !string.IsNullOrWhiteSpace(tpl.SinhalaFontFamily) ? tpl.SinhalaFontFamily : "Nirmala UI";

        if (tpl.LayoutType == LabelLayoutType.FoodBakery)
        {
            RenderFoodBakeryLayout(g, job, tpl, widthPx, heightPx, mmToPx, primaryFont, sinhalaFont);
        }
        else if (tpl.LayoutType == LabelLayoutType.RetailBarcode)
        {
            RenderRetailBarcodeLayout(g, job, tpl, widthPx, heightPx, mmToPx, primaryFont, sinhalaFont);
        }
        else
        {
            RenderFoodBakeryLayout(g, job, tpl, widthPx, heightPx, mmToPx, primaryFont, sinhalaFont);
        }

        // Apply trial watermark ONLY when printing to a printer, keeping the on-screen display clean!
        if (AppConfig.IsTrial && isForPrinting)
        {
            DrawTrialWatermark(g, widthPx, heightPx, mmToPx);
        }

        return bmp;
    }

    private static void RenderFoodBakeryLayout(Graphics g, PrintJob job, LabelTemplate tpl, int W, int H, float mmToPx, string primaryFont, string sinhalaFont)
    {
        // Responsive base scaling according to reference (40x45mm)
        float scaleW = tpl.WidthMm / 40.0f;
        float scaleH = tpl.HeightMm / 45.0f;
        float scale = Math.Clamp(Math.Min(scaleW, scaleH), 0.5f, 2.5f);

        float padX = Math.Max(6, tpl.MarginMm * mmToPx);
        float padY = Math.Max(4, tpl.MarginMm * mmToPx);
        float innerW = W - padX * 2;

        // Check vertical side text (Registration No)
        float leftMarginReserve = 0f;
        var regLayout = tpl.LayoutReg ?? new ElementLayout { RotationDeg = 90 };
        bool hasSideText = tpl.ShowRegistration && regLayout.Visible && !string.IsNullOrWhiteSpace(job.RegistrationNo);

        float sidePt = regLayout.FontSize > 0 ? regLayout.FontSize : Math.Max(4.2f, 4.8f * scale);
        string sideFontName = !string.IsNullOrEmpty(regLayout.FontFamily) ? regLayout.FontFamily : sinhalaFont;

        if (hasSideText)
        {
            bool isRegBold = regLayout.Bold ?? false;
            using var testFont = FontManager.CreateFont(sideFontName, sidePt, isRegBold ? FontStyle.Bold : FontStyle.Regular);
            var sideSize = g.MeasureString(job.RegistrationNo, testFont);

            if (regLayout.RotationDeg == 90 || regLayout.RotationDeg == 270)
            {
                leftMarginReserve = sideSize.Height + (8f * scale);
                float sideX = padX + (regLayout.OffsetXMm * mmToPx);
                float sideY = padY + 2f + (regLayout.OffsetYMm * mmToPx);
                DrawRotatedPhrase(g, job.RegistrationNo, testFont, Brushes.Black, sideX, sideY, regLayout.RotationDeg);
            }
            else
            {
                float sideX = padX + (regLayout.OffsetXMm * mmToPx);
                float sideY = padY + (regLayout.OffsetYMm * mmToPx);
                DrawRotatedPhrase(g, job.RegistrationNo, testFont, Brushes.Black, sideX, sideY, regLayout.RotationDeg);
            }
        }

        float contentX = padX + leftMarginReserve;
        float contentW = innerW - leftMarginReserve;
        float curY = padY;

        // 1. Title Section
        var titleLayout = tpl.LayoutTitle ?? new ElementLayout();
        var sinhalaLayout = tpl.LayoutSinhalaTitle ?? new ElementLayout();

        float titlePt = titleLayout.FontSize > 0 ? titleLayout.FontSize : Math.Max(7.0f, (tpl.TitleFontSize > 0 ? tpl.TitleFontSize : 9.2f) * scale);
        string tFont = !string.IsNullOrEmpty(titleLayout.FontFamily) ? titleLayout.FontFamily : primaryFont;
        bool isTitleBold = titleLayout.Bold ?? true;
        using var titleFont = FontManager.CreateFont(tFont, titlePt, isTitleBold ? FontStyle.Bold : FontStyle.Regular);

        float sTitlePt = sinhalaLayout.FontSize > 0 ? sinhalaLayout.FontSize : Math.Max(6.2f, (titlePt - 1.2f));
        string sFont = !string.IsNullOrEmpty(sinhalaLayout.FontFamily) ? sinhalaLayout.FontFamily : sinhalaFont;
        bool isSinhalaBold = sinhalaLayout.Bold ?? true;
        using var sinhalaTitleFont = FontManager.CreateFont(sFont, sTitlePt, isSinhalaBold ? FontStyle.Bold : FontStyle.Regular);

        string englishTitle = (titleLayout.Visible) ? (job.ProductName ?? "") : "";
        string sinhalaTitle = (tpl.ShowSinhalaName && sinhalaLayout.Visible && !string.IsNullOrWhiteSpace(job.SinhalaName)) ? job.SinhalaName : "";

        if (!string.IsNullOrEmpty(englishTitle))
        {
            float ex = contentX + (titleLayout.OffsetXMm * mmToPx);
            float ey = curY + (titleLayout.OffsetYMm * mmToPx);
            var titleSize = g.MeasureString(englishTitle, titleFont);
            DrawRotatedPhrase(g, englishTitle, titleFont, Brushes.Black, ex, ey, titleLayout.RotationDeg);
            curY += titleSize.Height - 1;
        }

        if (!string.IsNullOrEmpty(sinhalaTitle))
        {
            float sx = contentX + (sinhalaLayout.OffsetXMm * mmToPx);
            float sy = curY + (sinhalaLayout.OffsetYMm * mmToPx);
            var sinhalaSize = g.MeasureString(sinhalaTitle, sinhalaTitleFont);
            DrawRotatedPhrase(g, sinhalaTitle, sinhalaTitleFont, Brushes.Black, sx, sy, sinhalaLayout.RotationDeg);
            curY += sinhalaSize.Height + (2.5f * scale);
        }
        else if (!string.IsNullOrEmpty(englishTitle))
        {
            curY += (2.5f * scale);
        }

        // 2. Metadata Rows (M:DATE, EXP:DATE, BATCH, PRICE, WEIGHT)
        float baseMetaPt = Math.Max(4.8f, (tpl.BodyFontSize > 0 ? tpl.BodyFontSize : 5.8f) * scale);

        void DrawPhraseRow(string label, string val, ElementLayout layout, bool enabled)
        {
            if (!enabled || !layout.Visible || string.IsNullOrWhiteSpace(val)) return;

            float pt = layout.FontSize > 0 ? layout.FontSize : baseMetaPt;
            string fName = !string.IsNullOrEmpty(layout.FontFamily) ? layout.FontFamily : sinhalaFont;
            string vName = !string.IsNullOrEmpty(layout.FontFamily) ? layout.FontFamily : primaryFont;

            bool isRowBold = layout.Bold ?? true;
            using var mFont = FontManager.CreateFont(fName, pt, isRowBold ? FontStyle.Bold : FontStyle.Regular);
            using var valF = FontManager.CreateFont(vName, pt, isRowBold ? FontStyle.Bold : FontStyle.Regular);

            float rx = contentX + (layout.OffsetXMm * mmToPx);
            float ry = curY + (layout.OffsetYMm * mmToPx);
            float rHeight = mFont.GetHeight(g) + (1.8f * scale);

            if (layout.RotationDeg == 0)
            {
                var labelSize = g.MeasureString(label, mFont);
                var valSize = g.MeasureString(val, valF);
                g.DrawString(label, mFont, Brushes.Black, new PointF(rx, ry));

                float valX = contentX + contentW - valSize.Width + (layout.OffsetXMm * mmToPx);
                float minGap = 3f * scale;
                if (valX < rx + labelSize.Width + minGap)
                {
                    valX = rx + labelSize.Width + minGap;
                }
                g.DrawString(val, valF, Brushes.Black, new PointF(valX, ry));
            }
            else
            {
                string combined = $"{label}  {val}";
                DrawRotatedPhrase(g, combined, mFont, Brushes.Black, rx, ry, layout.RotationDeg);
            }

            curY += rHeight;
        }

        DrawPhraseRow("M:DATE - නි: දිනය", job.ManufactureDate.ToString("yyyy/MM/dd"), tpl.LayoutMDate ?? new(), tpl.ShowManufactureDate);
        DrawPhraseRow("EXP : DATE ප්‍ර: දිනය", job.ExpiryDate.ToString("yyyy/MM/dd"), tpl.LayoutExpDate ?? new(), tpl.ShowExpiryDate);
        DrawPhraseRow("BATCH NO - කා: අංකය", job.BatchNumber, tpl.LayoutBatch ?? new(), tpl.ShowBatchNo);
        DrawPhraseRow("PRICE මිල", job.PriceText, tpl.LayoutPrice ?? new(), tpl.ShowPrice);
        DrawPhraseRow("Weight බර", job.WeightText, tpl.LayoutWeight ?? new(), tpl.ShowWeight);

        curY += (2.5f * scale);

        // 3. Telephone Line (Calculated at bottom)
        var telLayout = tpl.LayoutTel ?? new ElementLayout();
        float telPt = telLayout.FontSize > 0 ? telLayout.FontSize : Math.Max(5.5f, 6.8f * scale);
        string telFontFamily = !string.IsNullOrEmpty(telLayout.FontFamily) ? telLayout.FontFamily : primaryFont;
        bool isTelBold = telLayout.Bold ?? true;
        using var telF = FontManager.CreateFont(telFontFamily, telPt, isTelBold ? FontStyle.Bold : FontStyle.Regular);

        float telBoxH = (tpl.ShowTel && telLayout.Visible && !string.IsNullOrWhiteSpace(job.Tel)) ? (telF.GetHeight(g) + 3f) : 0f;

        // 4. Description / Ingredients
        var descLayout = tpl.LayoutDesc ?? new ElementLayout();
        if (tpl.ShowDescription && descLayout.Visible && !string.IsNullOrWhiteSpace(job.Description))
        {
            float descPt = descLayout.FontSize > 0 ? descLayout.FontSize : Math.Max(4.5f, 5.0f * scale);
            string dFont = !string.IsNullOrEmpty(descLayout.FontFamily) ? descLayout.FontFamily : sinhalaFont;
            bool isDescBold = descLayout.Bold ?? false;
            using var descFont = FontManager.CreateFont(dFont, descPt, isDescBold ? FontStyle.Bold : FontStyle.Regular);

            float descY = curY + (descLayout.OffsetYMm * mmToPx);
            float descX = contentX + (descLayout.OffsetXMm * mmToPx);
            float availH = H - padY - telBoxH - descY;

            if (availH > 8)
            {
                if (descLayout.RotationDeg == 0)
                {
                    var descRect = new RectangleF(descX, descY, contentW, availH);
                    g.DrawString(job.Description, descFont, Brushes.Black, descRect, new StringFormat
                    {
                        Alignment = StringAlignment.Near,
                        LineAlignment = StringAlignment.Near,
                        Trimming = StringTrimming.EllipsisWord
                    });
                }
                else
                {
                    DrawRotatedPhrase(g, job.Description, descFont, Brushes.Black, descX, descY, descLayout.RotationDeg);
                }
            }
        }

        // Draw Telephone
        if (tpl.ShowTel && telLayout.Visible && !string.IsNullOrWhiteSpace(job.Tel))
        {
            float ty = H - padY - telBoxH + (telLayout.OffsetYMm * mmToPx);
            float tx = contentX + (telLayout.OffsetXMm * mmToPx);
            if (telLayout.RotationDeg == 0)
            {
                g.DrawString(job.Tel, telF, Brushes.Black, new RectangleF(tx, ty, contentW, telBoxH), new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                });
            }
            else
            {
                DrawRotatedPhrase(g, job.Tel, telF, Brushes.Black, tx, ty, telLayout.RotationDeg);
            }
        }
    }

    public static void DrawRotatedPhrase(Graphics g, string text, Font font, Brush brush, float x, float y, int rotationDeg)
    {
        if (string.IsNullOrEmpty(text)) return;
        rotationDeg = ((rotationDeg % 360) + 360) % 360;

        if (rotationDeg == 0)
        {
            g.DrawString(text, font, brush, new PointF(x, y));
            return;
        }

        var state = g.Save();
        try
        {
            var size = g.MeasureString(text, font);
            switch (rotationDeg)
            {
                case 90:
                    // Text reads downwards top-to-bottom.
                    // Local +X = screen +Y, local +Y = screen -X.
                    // Translate x by font height to keep text within [x .. x + height].
                    g.TranslateTransform(x + size.Height * 1.05f, y);
                    g.RotateTransform(90);
                    break;

                case 180:
                    // Text is upside down.
                    // Local +X = screen -X, local +Y = screen -Y.
                    g.TranslateTransform(x + size.Width, y + size.Height);
                    g.RotateTransform(180);
                    break;

                case 270:
                    // Text reads upwards bottom-to-top.
                    // Local +X = screen -Y, local +Y = screen +X.
                    g.TranslateTransform(x, y + size.Width);
                    g.RotateTransform(270);
                    break;

                default:
                    g.TranslateTransform(x, y);
                    g.RotateTransform(rotationDeg);
                    break;
            }
            g.DrawString(text, font, brush, new PointF(0, 0));
        }
        finally
        {
            g.Restore(state);
        }
    }

    private static void RenderRetailBarcodeLayout(Graphics g, PrintJob job, LabelTemplate tpl, int W, int H, float mmToPx, string primaryFont, string sinhalaFont)
    {
        float padX = Math.Max(8, tpl.MarginMm * mmToPx);
        float padY = Math.Max(6, tpl.MarginMm * mmToPx);
        float innerW = W - padX * 2;
        float curY = padY;

        // Title
        using var nameFont = FontManager.CreateFont(primaryFont, tpl.TitleFontSize, FontStyle.Bold);
        g.DrawString(job.ProductName, nameFont, Brushes.Black, new RectangleF(padX, curY, innerW, nameFont.GetHeight(g) + 2), new StringFormat
        {
            Alignment = StringAlignment.Near,
            Trimming = StringTrimming.EllipsisCharacter
        });
        curY += nameFont.GetHeight(g) + 2;

        if (tpl.ShowSinhalaName && !string.IsNullOrWhiteSpace(job.SinhalaName))
        {
            using var sinhalaFontInst = FontManager.CreateFont(sinhalaFont, tpl.TitleFontSize - 1.5f, FontStyle.Regular);
            g.DrawString(job.SinhalaName, sinhalaFontInst, Brushes.Black, new RectangleF(padX, curY, innerW, sinhalaFontInst.GetHeight(g) + 2), new StringFormat
            {
                Alignment = StringAlignment.Near,
                Trimming = StringTrimming.EllipsisCharacter
            });
            curY += sinhalaFontInst.GetHeight(g) + 2;
        }

        // Price Row
        if (tpl.ShowPrice && !string.IsNullOrWhiteSpace(job.PriceText))
        {
            using var priceFont = FontManager.CreateFont(primaryFont, 11f, FontStyle.Bold);
            g.DrawString(job.PriceText, priceFont, Brushes.Black, new PointF(padX, curY));
            curY += priceFont.GetHeight(g) + 4;
        }

        // Barcode
        string barcodeData = !string.IsNullOrWhiteSpace(job.Barcode) ? job.Barcode : job.BatchNumber;
        if (tpl.ShowBarcode && !string.IsNullOrWhiteSpace(barcodeData))
        {
            using var codeFont = FontManager.CreateFont("Courier New", 6.5f, FontStyle.Regular);
            float codeTextH = codeFont.GetHeight(g) + 1;
            float barcodeH = Math.Max(15, H - curY - padY - codeTextH);

            DrawBarcode(g, barcodeData, padX, curY, innerW, barcodeH);
            curY += barcodeH + 1;

            g.DrawString(barcodeData, codeFont, Brushes.Black, new RectangleF(padX, curY, innerW, codeTextH), new StringFormat
            {
                Alignment = StringAlignment.Center
            });
        }
    }

    private static void DrawBarcode(Graphics g, string data, float x, float y, float widthPx, float heightPx)
    {
        var (bars, totalModules) = BarcodeEncoder.EncodeCode128(data);
        if (totalModules == 0) return;
        float scale = widthPx / totalModules;
        foreach (var (bx, bw) in bars)
        {
            g.FillRectangle(Brushes.Black, x + bx * scale, y, Math.Max(1f, bw * scale), heightPx);
        }
    }

    /// <summary>
    /// Renders realistic WYSIWYG preview of sticker on thermal roll backing paper with drop shadow.
    /// </summary>
    public static Bitmap RenderPreview(PrintJob job, int targetW, int targetH)
    {
        var previewBmp = new Bitmap(targetW, targetH, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(previewBmp);
        g.Clear(Color.FromArgb(240, 242, 248)); // Soft neutral background
        g.SmoothingMode = SmoothingMode.HighQuality;

        using var labelBmp = RenderLabel(job, 0, isForPrinting: false);

        // Calculate aspect fit
        float margin = 24f;
        float availW = targetW - margin * 2;
        float availH = targetH - margin * 2;

        float scaleW = availW / labelBmp.Width;
        float scaleH = availH / labelBmp.Height;
        float scale = Math.Min(scaleW, scaleH);

        int drawW = (int)(labelBmp.Width * scale);
        int drawH = (int)(labelBmp.Height * scale);
        int drawX = (targetW - drawW) / 2;
        int drawY = (targetH - drawH) / 2;

        // Draw Roll backing paper strip (simulates sticker liner strip like in user photo!)
        int stripW = drawW + 36;
        int stripX = (targetW - stripW) / 2;
        using (var linerBrush = new SolidBrush(Color.FromArgb(248, 248, 245)))
        using (var linerPen = new Pen(Color.FromArgb(220, 224, 230), 1f))
        {
            g.FillRectangle(linerBrush, stripX, 0, stripW, targetH);
            g.DrawLine(linerPen, stripX, 0, stripX, targetH);
            g.DrawLine(linerPen, stripX + stripW, 0, stripX + stripW, targetH);
        }

        // Draw Sticker Drop Shadow
        using (var shadowBrush = new SolidBrush(Color.FromArgb(40, 0, 0, 0)))
        {
            FillRoundedRect(g, shadowBrush, drawX + 3, drawY + 4, drawW, drawH, 10);
        }

        // Draw Sticker White Body
        using (var whiteBrush = new SolidBrush(Color.White))
        {
            FillRoundedRect(g, whiteBrush, drawX, drawY, drawW, drawH, 10);
        }

        // Draw Label content inside clipped rounded sticker
        var state = g.Save();
        using (var path = GetRoundedRectPath(drawX, drawY, drawW, drawH, 10))
        {
            g.SetClip(path);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(labelBmp, new Rectangle(drawX, drawY, drawW, drawH));
        }
        g.Restore(state);

        // Draw sticker subtle border
        using (var borderPen = new Pen(Color.FromArgb(210, 215, 225), 1.2f))
        {
            DrawRoundedRect(g, borderPen, drawX, drawY, drawW, drawH, 10);
        }

        // Draw Dimension tag banner
        string sizeText = $"{job.Template.WidthMm:0.#} mm × {job.Template.HeightMm:0.#} mm";
        using var tagFont = new Font("Segoe UI", 8f, FontStyle.Bold);
        var tagSize = g.MeasureString(sizeText, tagFont);
        int tagX = drawX + (drawW - (int)tagSize.Width - 16) / 2;
        int tagY = drawY + drawH + 8;
        if (tagY + 20 < targetH)
        {
            using var tagBg = new SolidBrush(Color.FromArgb(230, 235, 245));
            using var tagPen = new Pen(Color.FromArgb(180, 190, 210), 1f);
            FillRoundedRect(g, tagBg, tagX, tagY, (int)tagSize.Width + 16, (int)tagSize.Height + 6, 6);
            DrawRoundedRect(g, tagPen, tagX, tagY, (int)tagSize.Width + 16, (int)tagSize.Height + 6, 6);
            g.DrawString(sizeText, tagFont, Brushes.DimGray, tagX + 8, tagY + 3);
        }

        return previewBmp;
    }

    private static GraphicsPath GetRoundedRectPath(float x, float y, float w, float h, float r)
    {
        var path = new GraphicsPath();
        float d = r * 2;
        path.AddArc(x, y, d, d, 180, 90);
        path.AddArc(x + w - d, y, d, d, 270, 90);
        path.AddArc(x + w - d, y + h - d, d, d, 0, 90);
        path.AddArc(x, y + h - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static void FillRoundedRect(Graphics g, Brush brush, float x, float y, float w, float h, float r)
    {
        using var path = GetRoundedRectPath(x, y, w, h, r);
        g.FillPath(brush, path);
    }

    private static void DrawRoundedRect(Graphics g, Pen pen, float x, float y, float w, float h, float r)
    {
        using var path = GetRoundedRectPath(x, y, w, h, r);
        g.DrawPath(pen, path);
    }

    private static void DrawTrialWatermark(Graphics g, int W, int H, float mmToPx)
    {
        // 1. Top and Bottom warning banners
        int barH = (int)Math.Max(16, H * 0.042f);
        using var smallFont = new Font("Segoe UI", barH * 0.52f, FontStyle.Bold, GraphicsUnit.Pixel);

        // Top warning border strip
        using (var topBrush = new SolidBrush(Color.Black))
        {
            g.FillRectangle(topBrush, 0, 0, W, barH);
            g.DrawString("★ LUNAPOS TRIAL DEMO ★", smallFont, Brushes.White, new RectangleF(0, 0, W, barH), new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
        }

        // Bottom warning border strip
        using (var botBrush = new SolidBrush(Color.Black))
        {
            g.FillRectangle(botBrush, 0, H - barH, W, barH);
            g.DrawString("★ NOT FOR SALE • 30-MIN DEMO ★", smallFont, Brushes.White, new RectangleF(0, H - barH, W, barH), new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
        }

        // 2. Center Large Diagonal Watermark Banner
        var state = g.Save();
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // Move to center of label and rotate
        g.TranslateTransform(W / 2f, H / 2f);
        g.RotateTransform(-26f);

        float bannerW = (float)Math.Sqrt(W * W + H * H) * 0.96f;
        float bannerH = Math.Max(28f, H * 0.11f);

        // Draw double parallel barrier lines across center
        using var diagPen = new Pen(Color.Black, Math.Max(2f, bannerH * 0.05f));
        g.DrawLine(diagPen, -bannerW / 2f, -bannerH / 2f, bannerW / 2f, -bannerH / 2f);
        g.DrawLine(diagPen, -bannerW / 2f, bannerH / 2f, bannerW / 2f, bannerH / 2f);

        // Fill background with subtle hatched pattern
        using var hatch = new HatchBrush(HatchStyle.Percent20, Color.Black, Color.Transparent);
        g.FillRectangle(hatch, -bannerW / 2f, -bannerH / 2f, bannerW, bannerH);

        float emMain = bannerH * 0.40f;
        float emSub = bannerH * 0.25f;

        var mainFormat = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        var subFormat = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

        var mainRect = new RectangleF(-bannerW / 2f, -bannerH / 2f + 1, bannerW, bannerH * 0.54f);
        var subRect = new RectangleF(-bannerW / 2f, 0, bannerW, bannerH * 0.46f);

        // Draw text with white halo for maximum contrast on any printed content
        using var haloPen = new Pen(Color.White, Math.Max(2f, bannerH * 0.07f));
        using var gpMain = new GraphicsPath();
        gpMain.AddString("TRIAL VERSION - LUNAPOS", FontFamily.GenericSansSerif, (int)FontStyle.Bold, emMain, mainRect, mainFormat);
        g.DrawPath(haloPen, gpMain);
        g.FillPath(Brushes.Black, gpMain);

        using var gpSub = new GraphicsPath();
        gpSub.AddString("EVALUATION ONLY • NOT FOR SALE", FontFamily.GenericSansSerif, (int)FontStyle.Bold, emSub, subRect, subFormat);
        g.DrawPath(haloPen, gpSub);
        g.FillPath(Brushes.Black, gpSub);

        g.Restore(state);
    }
}

public class PrintingEngine
{
    public static void Print(PrintJob job, AppSettings settings)
    {
        if (settings.ConnectionType == PrinterConnectionType.BluetoothSerial)
        {
            if (string.IsNullOrWhiteSpace(settings.BluetoothPort))
                throw new ArgumentException("No Bluetooth / COM port selected in Settings.");

            PrintViaSerial(job, settings.BluetoothPort, settings.BluetoothBaudRate, settings.PrintEngine);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(settings.SelectedPrinter))
                throw new ArgumentException("No printer selected in Settings.");

            Print(job, settings.SelectedPrinter, settings.PrintEngine);
        }
    }

    public static void Print(PrintJob job, string printerName, PrintEngine engine)
    {
        if (string.IsNullOrWhiteSpace(printerName))
            throw new ArgumentException("No printer selected.");

        if (AppConfig.IsTrial)
        {
            if (TrialManager.IsTrialExpired)
            {
                throw new InvalidOperationException("Trial period has expired on this device. Printing is disabled.");
            }

            if (job.Copies > 3)
            {
                job.Copies = 3;
            }
        }

        switch (engine)
        {
            case PrintEngine.WindowsDriver:
                PrintViaWindowsDriver(job, printerName);
                break;
            case PrintEngine.TsplRaw:
                PrintViaTspl(job, printerName);
                break;
            case PrintEngine.EscPosRaster:
                PrintViaEscPos(job, printerName);
                break;
            default:
                PrintViaWindowsDriver(job, printerName);
                break;
        }
    }

    public static void PrintViaSerial(PrintJob job, string portName, int baudRate, PrintEngine engine)
    {
        if (string.IsNullOrWhiteSpace(portName))
            throw new ArgumentException("No Bluetooth / COM port specified.");

        if (AppConfig.IsTrial)
        {
            if (TrialManager.IsTrialExpired)
            {
                throw new InvalidOperationException("Trial period has expired on this device. Printing is disabled.");
            }

            if (job.Copies > 3)
            {
                job.Copies = 3;
            }
        }

        byte[] bytes;
        if (engine == PrintEngine.EscPosRaster)
        {
            var single = BuildEscPosBytes(job);
            if (job.Copies <= 1)
            {
                bytes = single;
            }
            else
            {
                var combined = new List<byte>(single.Length * job.Copies);
                for (int i = 0; i < job.Copies; i++) combined.AddRange(single);
                bytes = combined.ToArray();
            }
        }
        else
        {
            // Default to TSPL raw for label printers
            bytes = BuildTsplBytes(job);
        }

        BluetoothManager.SendRawBytes(portName, baudRate, bytes);
    }

    private static void PrintViaWindowsDriver(PrintJob job, string printerName)
    {
        var tpl = job.Template ?? new LabelTemplate();

        using var pd = new PrintDocument();
        pd.PrinterSettings.PrinterName = printerName;
        pd.PrinterSettings.Copies = (short)Math.Max(1, job.Copies);

        // Convert mm to hundredths of an inch (1 inch = 25.4 mm)
        int paperW = (int)Math.Round((tpl.WidthMm / 25.4) * 100);
        int paperH = (int)Math.Round((tpl.HeightMm / 25.4) * 100);

        pd.DefaultPageSettings.PaperSize = new PaperSize("Custom Label", paperW, paperH);
        pd.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);
        pd.OriginAtMargins = false;

        using var bmp = LabelRenderer.RenderLabel(job, 300, isForPrinting: true); // 300 DPI high resolution for driver

        pd.PrintPage += (s, e) =>
        {
            if (e.Graphics != null)
            {
                e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
                e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                e.Graphics.DrawImage(bmp, e.PageBounds);
            }
            e.HasMorePages = false;
        };

        pd.Print();
    }

    public static byte[] BuildTsplBytes(PrintJob job)
    {
        var tpl = job.Template ?? new LabelTemplate();
        int dpi = tpl.Dpi > 0 ? tpl.Dpi : 203;

        using var bmp = LabelRenderer.RenderLabel(job, dpi, isForPrinting: true);
        int w = bmp.Width;
        int h = bmp.Height;
        int widthBytes = (w + 7) / 8;

        var sb = new StringBuilder();
        sb.AppendLine($"SIZE {(int)tpl.WidthMm} mm, {(int)tpl.HeightMm} mm");
        sb.AppendLine("GAP 2 mm, 0 mm");
        sb.AppendLine("DIRECTION 1");
        sb.AppendLine("CLS");
        sb.Append($"BITMAP 0,0,{widthBytes},{h},0,");

        // Convert bitmap to monochrome 1-bit raster
        byte[] rasterData = new byte[widthBytes * h];
        for (int y = 0; y < h; y++)
        {
            for (int bx = 0; bx < widthBytes; bx++)
            {
                byte b = 0;
                for (int bit = 0; bit < 8; bit++)
                {
                    int px = bx * 8 + bit;
                    if (px < w)
                    {
                        var c = bmp.GetPixel(px, y);
                        // TSPL BITMAP 0 is black, 1 is white
                        if ((c.R + c.G + c.B) / 3 < 128)
                        {
                            b |= (byte)(0x80 >> bit);
                        }
                    }
                }
                rasterData[y * widthBytes + bx] = (byte)~b; // Inverted for TSPL mode 0
            }
        }

        byte[] headerBytes = Encoding.ASCII.GetBytes(sb.ToString());
        byte[] footerBytes = Encoding.ASCII.GetBytes($"\r\nPRINT {job.Copies},1\r\n");

        byte[] totalBytes = new byte[headerBytes.Length + rasterData.Length + footerBytes.Length];
        Buffer.BlockCopy(headerBytes, 0, totalBytes, 0, headerBytes.Length);
        Buffer.BlockCopy(rasterData, 0, totalBytes, headerBytes.Length, rasterData.Length);
        Buffer.BlockCopy(footerBytes, 0, totalBytes, headerBytes.Length + rasterData.Length, footerBytes.Length);
        return totalBytes;
    }

    private static void PrintViaTspl(PrintJob job, string printerName)
    {
        byte[] totalBytes = BuildTsplBytes(job);
        RawPrinterHelper.SendBytes(printerName, totalBytes);
    }

    public static byte[] BuildEscPosBytes(PrintJob job)
    {
        var tpl = job.Template ?? new LabelTemplate();
        int dpi = tpl.Dpi > 0 ? tpl.Dpi : 203;

        using var bmp = LabelRenderer.RenderLabel(job, dpi, isForPrinting: true);
        int w = bmp.Width;
        int h = bmp.Height;
        int bytesPerRow = (w + 7) / 8;

        var buf = new List<byte>(bytesPerRow * h + 64);
        buf.AddRange([0x1B, 0x40]); // ESC @ Initialize
        buf.AddRange([0x1D, 0x76, 0x30, 0x00]); // GS v 0 raster command
        buf.Add((byte)(bytesPerRow & 0xFF));
        buf.Add((byte)((bytesPerRow >> 8) & 0xFF));
        buf.Add((byte)(h & 0xFF));
        buf.Add((byte)((h >> 8) & 0xFF));

        for (int y = 0; y < h; y++)
        {
            for (int bx = 0; bx < bytesPerRow; bx++)
            {
                byte b = 0;
                for (int bit = 0; bit < 8; bit++)
                {
                    int px = bx * 8 + bit;
                    if (px < w)
                    {
                        var c = bmp.GetPixel(px, y);
                        if ((c.R + c.G + c.B) / 3 < 128)
                            b |= (byte)(0x80 >> bit);
                    }
                }
                buf.Add(b);
            }
        }
        buf.Add(0x0C); // FF Form feed / cut
        return buf.ToArray();
    }

    private static void PrintViaEscPos(PrintJob job, string printerName)
    {
        byte[] raster = BuildEscPosBytes(job);
        for (int i = 0; i < job.Copies; i++)
        {
            RawPrinterHelper.SendBytes(printerName, raster);
        }
    }
}
