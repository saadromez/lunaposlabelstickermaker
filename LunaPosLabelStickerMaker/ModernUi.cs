using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LunaPosLabelStickerMaker;

public static class ModernColors
{
    public static readonly Color AppBackground   = Color.FromArgb(244, 246, 251);
    public static readonly Color CardBackground  = Color.White;
    public static readonly Color SidebarBg       = Color.FromArgb(24, 26, 42);
    public static readonly Color SidebarHover    = Color.FromArgb(36, 39, 64);
    public static readonly Color SidebarActive   = Color.FromArgb(99, 102, 241);
    
    public static readonly Color Primary         = Color.FromArgb(99, 102, 241); // Indigo / Purple
    public static readonly Color PrimaryHover    = Color.FromArgb(79, 70, 229);
    public static readonly Color PrimaryLight    = Color.FromArgb(238, 242, 255);
    
    public static readonly Color AccentCyan      = Color.FromArgb(6, 182, 212);
    public static readonly Color AccentPink      = Color.FromArgb(236, 72, 153);
    public static readonly Color Success         = Color.FromArgb(16, 185, 129);
    public static readonly Color Warning         = Color.FromArgb(245, 158, 11);
    public static readonly Color Danger          = Color.FromArgb(239, 68, 68);

    public static readonly Color TextDark        = Color.FromArgb(30, 41, 59);
    public static readonly Color TextMuted       = Color.FromArgb(100, 116, 139);
    public static readonly Color TextLight       = Color.FromArgb(148, 163, 184);

    public static readonly Color Border          = Color.FromArgb(226, 232, 240);
    public static readonly Color InputBackground = Color.FromArgb(248, 250, 252);
}

public class ModernCard : Panel
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int CornerRadius { get; set; } = 14;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color CardBorderColor { get; set; } = ModernColors.Border;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ShowBorder { get; set; } = true;

    public ModernCard()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        BackColor = ModernColors.CardBackground;
        Padding = new Padding(12);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
        using var path = CreateRoundedPath(rect, CornerRadius);

        using var bgBrush = new SolidBrush(BackColor);
        e.Graphics.FillPath(bgBrush, path);

        if (ShowBorder)
        {
            using var pen = new Pen(CardBorderColor, 1.2f);
            e.Graphics.DrawPath(pen, path);
        }
    }

    private static GraphicsPath CreateRoundedPath(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        float d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

public class ModernButton : Button
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int CornerRadius { get; set; } = 10;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color DefaultBg { get; set; } = ModernColors.Primary;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color HoverBg { get; set; } = ModernColors.PrimaryHover;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color PressedBg { get; set; } = Color.FromArgb(67, 56, 202);

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color CustomBorderColor { get; set; } = Color.Transparent;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsOutlined { get; set; } = false;

    private bool _isHovered;
    private bool _isPressed;

    public ModernButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        ForeColor = Color.White;
        Cursor = Cursors.Hand;
        Size = new Size(120, 38);
    }

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _isHovered = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _isHovered = false; Invalidate(); }
    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); _isPressed = true; Invalidate(); }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); _isPressed = false; Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = new RectangleF(1f, 1f, Width - 2f, Height - 2f);
        using var path = CreateRoundedPath(rect, CornerRadius);

        Color currentBg = _isPressed ? PressedBg : (_isHovered ? HoverBg : DefaultBg);
        if (IsOutlined)
        {
            using var bg = new SolidBrush(BackColor);
            e.Graphics.FillPath(bg, path);
            using var borderPen = new Pen(CustomBorderColor != Color.Transparent ? CustomBorderColor : DefaultBg, 1.5f);
            e.Graphics.DrawPath(borderPen, path);
        }
        else
        {
            using var bg = new SolidBrush(currentBg);
            e.Graphics.FillPath(bg, path);
            if (CustomBorderColor != Color.Transparent)
            {
                using var borderPen = new Pen(CustomBorderColor, 1.2f);
                e.Graphics.DrawPath(borderPen, path);
            }
        }

        // Draw Text
        var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine;
        TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ForeColor, flags);
    }

    private static GraphicsPath CreateRoundedPath(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        float d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

public class PillChip : Control
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Selected { get; set; } = false;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color ActiveBg { get; set; } = ModernColors.PrimaryLight;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color ActiveText { get; set; } = ModernColors.Primary;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color NormalBg { get; set; } = Color.FromArgb(241, 245, 249);

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color NormalText { get; set; } = ModernColors.TextDark;

    public PillChip()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Cursor = Cursors.Hand;
        Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        Size = new Size(60, 28);
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        float r = Height / 2f - 1f;
        var rect = new RectangleF(1f, 1f, Width - 2f, Height - 2f);
        using var path = new GraphicsPath();
        float d = r * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();

        using var bg = new SolidBrush(Selected ? ActiveBg : NormalBg);
        e.Graphics.FillPath(bg, path);

        using var borderPen = new Pen(Selected ? ActiveText : ModernColors.Border, 1.2f);
        e.Graphics.DrawPath(borderPen, path);

        var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine;
        TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, Selected ? ActiveText : NormalText, flags);
    }
}

public class SidebarTabButton : Control
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string TabId { get; set; } = "";

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string IconText { get; set; } = "🏷";

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsActive { get; set; } = false;

    private bool _isHovered;

    public SidebarTabButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Cursor = Cursors.Hand;
        Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        ForeColor = Color.FromArgb(200, 205, 225);
        Height = 44;
    }

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _isHovered = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _isHovered = false; Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        if (IsActive)
        {
            var rect = new RectangleF(8, 4, Width - 16, Height - 8);
            using var path = CreateRoundedPath(rect, 10);
            using var bg = new SolidBrush(ModernColors.SidebarActive);
            e.Graphics.FillPath(bg, path);
        }
        else if (_isHovered)
        {
            var rect = new RectangleF(8, 4, Width - 16, Height - 8);
            using var path = CreateRoundedPath(rect, 10);
            using var bg = new SolidBrush(ModernColors.SidebarHover);
            e.Graphics.FillPath(bg, path);
        }

        Color textCol = IsActive ? Color.White : (_isHovered ? Color.White : ForeColor);

        // Draw Icon
        using var iconFont = new Font("Segoe UI Emoji", 11f);
        e.Graphics.DrawString(IconText, iconFont, new SolidBrush(textCol), new PointF(18, 11));

        // Draw Label
        e.Graphics.DrawString(Text, Font, new SolidBrush(textCol), new PointF(46, 12));
    }

    private static GraphicsPath CreateRoundedPath(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        float d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
