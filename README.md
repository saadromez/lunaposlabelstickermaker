# LunaPOS Label Sticker Maker 🏷️

A professional desktop application for creating, managing, and printing high-precision product packaging stickers, food labels, and retail price tags with complete Sinhala & English typography support.

---

## 🌟 Key Features

1. **Standalone Product Management**
   - Create, edit, search, and manage products locally with full offline persistence.
   - Store English name, Sinhala name (`සිංහල නම`), category, default price, weight, shelf life (days), batch prefix, description/ingredients, registration number (`දි:ව ලියාපදිංචි`), phone, and barcode.
   - Pre-loaded with realistic Sri Lankan bakery/food templates (Sugar Biscuit / සීනි බිස්කට්, Butter Cake, Kithul Hakuru, Spicy Mixture, Chocolate Roll, Milk Toffee).

2. **On-the-Fly Live Customization Before Printing**
   - Pick a product and modify manufacture dates (`M:DATE`), expiry dates (`EXP:DATE`), batch numbers (`BATCH NO`), prices, and weights directly on the print screen with single-click quick chips (`[Today]`, `[Yesterday]`, `[+14d]`, `[+30d]`, `[+90d]`, `[1]`, `[5]`, `[10]`, `[25]`, `[50]`, `[100]` copies).

3. **WYSIWYG Realistic Label Preview**
   - Live visual preview displaying the physical sticker on roll liner backing with drop shadow, dimensions tag (`50mm × 40mm @ 203 DPI`), and real-time updates as you type.

4. **All Computer Fonts Supported**
   - Dynamically loads and supports all installed fonts from the computer (`Nirmala UI`, `Iskoola Pota`, `FMAbhaya`, `FMBindumathi`, `FMDerana`, `FMGanganee`, `Segoe UI`, `Arial`, etc.).
   - Separate font configuration for English titles, Sinhala text, and metadata.

5. **Universal Printer & Communication Engine**
   - **Windows Driver Mode (GDI Graphics)**: Universal high-precision vector/raster printing compatible with all Windows printer drivers (Xprinter XP-365B, XP-370B, XP-420B, 4BARCODE, Zebra GK420t, TSC, Rongta, Gprinter, etc.).
   - **TSPL Raw Command Mode**: Fast thermal transfer raster streaming.
   - **ESC/POS Raster Mode**: POS receipt printer support.

6. **Customizable Label Templates**
   - Visual template editor to define custom Width (mm), Height (mm), DPI, fonts, font sizes, and field visibility toggles.
   - Pre-configured:
     * Bakery Food Label (50x40mm) - Exact match to customer reference photo
     * Square Food Label (50x50mm)
     * Retail Price & Barcode Tag (40x30mm)
     * Compact Barcode Sticker (30x20mm)

7. **One-Click Windows Installer**
   - Ready-to-distribute setup installer created with Inno Setup.

---

## 🚀 Ready-to-Use Outputs

- **Windows Setup Installer**:  
  [`installer_output/LunaPOSLabelStickerMakerSetup.exe`](file:///c:/Users/dread/OneDrive/Attachments/Desktop/LUNAPOS%20CLIENT%20APPS(DEV)/lunpos%20label%20scticker%20maker/installer_output/LunaPOSLabelStickerMakerSetup.exe) (2.09 MB)

- **Published Standalone Executable**:  
  [`publish/LunaPosLabelStickerMaker.exe`](file:///c:/Users/dread/OneDrive/Attachments/Desktop/LUNAPOS%20CLIENT%20APPS(DEV)/lunpos%20label%20scticker%20maker/publish/LunaPosLabelStickerMaker.exe)

---

## 🛠️ Build Commands

To build and run from source:
```powershell
# Run the application
dotnet run --project LunaPosLabelStickerMaker

# Publish Release build
dotnet publish LunaPosLabelStickerMaker/LunaPosLabelStickerMaker.csproj -c Release -o publish

# Compile Inno Setup installer
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer.iss
```
