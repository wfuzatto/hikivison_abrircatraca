using System.Drawing.Drawing2D;

namespace HikvisionAbrirCatraca.Ui;

internal static class AppTheme
{
    public static readonly Color Background = Color.FromArgb(247, 250, 253);
    public static readonly Color Card = Color.White;
    public static readonly Color Navy = Color.FromArgb(18, 43, 87);
    public static readonly Color Muted = Color.FromArgb(104, 123, 151);
    public static readonly Color Blue = Color.FromArgb(11, 122, 231);
    public static readonly Color BlueHover = Color.FromArgb(8, 103, 200);
    public static readonly Color Red = Color.FromArgb(242, 35, 60);
    public static readonly Color RedHover = Color.FromArgb(215, 28, 49);
    public static readonly Color Green = Color.FromArgb(31, 181, 84);
    public static readonly Color GreenText = Color.FromArgb(21, 117, 55);
    public static readonly Color GreenSoft = Color.FromArgb(236, 249, 241);
    public static readonly Color Border = Color.FromArgb(225, 232, 241);
    public static readonly Color SoftBlue = Color.FromArgb(234, 245, 255);
    public static readonly Color Shadow = Color.FromArgb(24, 30, 55, 80);

    public static Font Font(float size, FontStyle style = FontStyle.Regular) =>
        new("Segoe UI", size, style, GraphicsUnit.Point);

    public static Font SemiBold(float size) =>
        new("Segoe UI Semibold", size, FontStyle.Regular, GraphicsUnit.Point);
}

internal static class RoundRect
{
    public static GraphicsPath Create(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Max(1, radius * 2);
        var arc = new Rectangle(rect.X, rect.Y, diameter, diameter);

        path.AddArc(arc, 180, 90);
        arc.X = rect.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = rect.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = rect.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();

        return path;
    }
}

internal sealed class CardPanel : Panel
{
    public int CornerRadius { get; set; } = 16;
    public bool ShadowEnabled { get; set; } = true;

    public CardPanel()
    {
        DoubleBuffered = true;
        BackColor = Color.Transparent;
        Resize += (_, _) => UpdateRegion();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var shadowOffset = ShadowEnabled ? 4 : 0;
        var cardRect = new Rectangle(2, 2, Width - 7, Height - 7);

        if (ShadowEnabled)
        {
            using var shadowPath = RoundRect.Create(
                new Rectangle(cardRect.X + shadowOffset, cardRect.Y + shadowOffset, cardRect.Width, cardRect.Height),
                CornerRadius);
            using var shadowBrush = new SolidBrush(Color.FromArgb(22, 37, 61, 92));
            e.Graphics.FillPath(shadowBrush, shadowPath);
        }

        using var path = RoundRect.Create(cardRect, CornerRadius);
        using var fill = new SolidBrush(AppTheme.Card);
        using var border = new Pen(AppTheme.Border, 1f);
        e.Graphics.FillPath(fill, path);
        e.Graphics.DrawPath(border, path);

        base.OnPaint(e);
    }

    private void UpdateRegion()
    {
        if (Width <= 0 || Height <= 0) return;
        using var path = RoundRect.Create(new Rectangle(0, 0, Width, Height), CornerRadius + 2);
        Region = new Region(path);
    }
}

internal class RoundedButton : Button
{
    private bool _hovered;

    public int CornerRadius { get; set; } = 10;
    public Color FillColor { get; set; } = AppTheme.Blue;
    public Color HoverColor { get; set; } = AppTheme.BlueHover;
    public Color BorderColor { get; set; } = Color.Transparent;
    public int BorderSize { get; set; }
    public Color DisabledFillColor { get; set; } = Color.FromArgb(190, 199, 211);

    public RoundedButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        Cursor = Cursors.Hand;
        ForeColor = Color.White;
        DoubleBuffered = true;
        TextAlign = ContentAlignment.MiddleCenter;
        MouseEnter += (_, _) => { _hovered = true; Invalidate(); };
        MouseLeave += (_, _) => { _hovered = false; Invalidate(); };
        Resize += (_, _) => UpdateRegion();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = RoundRect.Create(rect, CornerRadius);

        var fillColor = !Enabled
            ? DisabledFillColor
            : _hovered
                ? HoverColor
                : FillColor;

        using var fill = new SolidBrush(fillColor);
        e.Graphics.FillPath(fill, path);

        if (BorderSize > 0)
        {
            using var border = new Pen(BorderColor, BorderSize);
            e.Graphics.DrawPath(border, path);
        }

        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            rect,
            Enabled ? ForeColor : Color.WhiteSmoke,
            TextFormatFlags.HorizontalCenter |
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis);
    }

    private void UpdateRegion()
    {
        if (Width <= 0 || Height <= 0) return;
        using var path = RoundRect.Create(new Rectangle(0, 0, Width, Height), CornerRadius);
        Region = new Region(path);
    }
}

internal enum TurnstileIconVariant
{
    Generic,
    Entry,
    Exit,
    Shop,
    Header
}

internal sealed class TurnstileIcon : Control
{
    public TurnstileIconVariant Variant { get; set; } = TurnstileIconVariant.Generic;

    public TurnstileIcon()
    {
        DoubleBuffered = true;
        BackColor = Color.Transparent;
        Size = new Size(92, 92);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var g = e.Graphics;

        if (Variant != TurnstileIconVariant.Header)
        {
            using var bgPath = RoundRect.Create(new Rectangle(0, 0, Width - 1, Height - 1), 14);
            using var bg = new SolidBrush(AppTheme.SoftBlue);
            g.FillPath(bg, bgPath);
        }

        var scale = Variant == TurnstileIconVariant.Header ? 1.15f : 1f;
        var cx = Width / 2f;
        var baseY = Height * .70f;
        var postW = 12f * scale;
        var postH = 40f * scale;
        var leftX = cx - 24f * scale;
        var rightX = cx + 12f * scale;
        var topY = baseY - postH;

        using var dark = new SolidBrush(Color.FromArgb(55, 79, 109));
        using var mid = new SolidBrush(Color.FromArgb(90, 118, 148));
        using var blue = new SolidBrush(AppTheme.Blue);

        g.FillRectangle(dark, leftX, topY, postW, postH);
        g.FillRectangle(dark, rightX, topY, postW, postH);
        g.FillRectangle(mid, leftX + 2, topY + 2, 3, postH - 4);
        g.FillRectangle(mid, rightX + 2, topY + 2, 3, postH - 4);

        using var railPen = new Pen(Color.FromArgb(58, 85, 117), 4f * scale)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        g.DrawLine(railPen, leftX + postW, topY + 10 * scale, rightX, topY + 10 * scale);
        g.DrawLine(railPen, cx, topY + 10 * scale, cx, topY + 28 * scale);

        using var glow = new SolidBrush(Color.FromArgb(84, 191, 255));
        g.FillEllipse(glow, leftX + 4, topY + 8, 4 * scale, 4 * scale);
        g.FillEllipse(glow, rightX + 4, topY + 8, 4 * scale, 4 * scale);

        if (Variant is TurnstileIconVariant.Entry or TurnstileIconVariant.Exit)
        {
            var circle = new RectangleF(cx - 14 * scale, 4 * scale, 28 * scale, 28 * scale);
            g.FillEllipse(blue, circle);

            using var p = new Pen(Color.White, 3f * scale)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };

            if (Variant == TurnstileIconVariant.Entry)
            {
                g.DrawLine(p, cx, 24 * scale, cx, 12 * scale);
                g.DrawLine(p, cx, 12 * scale, cx - 5 * scale, 17 * scale);
                g.DrawLine(p, cx, 12 * scale, cx + 5 * scale, 17 * scale);
            }
            else
            {
                g.DrawLine(p, cx, 10 * scale, cx, 22 * scale);
                g.DrawLine(p, cx, 22 * scale, cx - 5 * scale, 17 * scale);
                g.DrawLine(p, cx, 22 * scale, cx + 5 * scale, 17 * scale);
            }
        }
        else if (Variant == TurnstileIconVariant.Shop)
        {
            var bag = new RectangleF(cx - 13 * scale, 4 * scale, 26 * scale, 26 * scale);
            using var bagPath = RoundRect.Create(Rectangle.Round(bag), 6);
            g.FillPath(blue, bagPath);

            using var handlePen = new Pen(Color.White, 2.4f * scale);
            g.DrawArc(handlePen, cx - 7 * scale, 1 * scale, 14 * scale, 13 * scale, 195, 150);
        }
        else if (Variant == TurnstileIconVariant.Header)
        {
            using var head = new Pen(AppTheme.Navy, 3.4f);
            g.DrawEllipse(head, cx - 4, 2, 8, 8);
            g.DrawLine(head, cx, 10, cx, 22);
            g.DrawLine(head, cx - 8, 16, cx + 8, 16);
        }
    }
}

internal sealed class StatusBanner : Control
{
    private string _message = "";
    private bool? _success;

    public string Message
    {
        get => _message;
        set { _message = value; Invalidate(); }
    }

    public bool? Success
    {
        get => _success;
        set { _success = value; Invalidate(); }
    }

    public StatusBanner()
    {
        DoubleBuffered = true;
        Height = 58;
        BackColor = Color.Transparent;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var fillColor = _success switch
        {
            true => AppTheme.GreenSoft,
            false => Color.FromArgb(254, 239, 241),
            _ => AppTheme.GreenSoft
        };
        var textColor = _success switch
        {
            true => AppTheme.GreenText,
            false => Color.FromArgb(169, 44, 58),
            _ => AppTheme.GreenText
        };
        var iconColor = _success switch
        {
            true => AppTheme.Green,
            false => AppTheme.Red,
            _ => AppTheme.Green
        };

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = RoundRect.Create(rect, 12);
        using var fill = new SolidBrush(fillColor);
        using var border = new Pen(Color.FromArgb(200, iconColor), 1);
        e.Graphics.FillPath(fill, path);
        e.Graphics.DrawPath(border, path);

        var circle = new Rectangle(18, 13, 32, 32);
        using var iconBrush = new SolidBrush(iconColor);
        e.Graphics.FillEllipse(iconBrush, circle);

        using var checkPen = new Pen(Color.White, 2.8f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };

        if (_success == false)
        {
            e.Graphics.DrawLine(checkPen, 28, 23, 40, 35);
            e.Graphics.DrawLine(checkPen, 40, 23, 28, 35);
        }
        else
        {
            e.Graphics.DrawLine(checkPen, 27, 29, 32, 34);
            e.Graphics.DrawLine(checkPen, 32, 34, 42, 23);
        }

        TextRenderer.DrawText(
            e.Graphics,
            _message,
            AppTheme.SemiBold(10.5f),
            new Rectangle(65, 0, Width - 80, Height),
            textColor,
            TextFormatFlags.Left |
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis);
    }
}

internal sealed class InfoIcon : Control
{
    public InfoIcon()
    {
        DoubleBuffered = true;
        Size = new Size(24, 24);
        BackColor = Color.Transparent;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(AppTheme.Muted, 1.8f);
        e.Graphics.DrawEllipse(pen, 2, 2, Width - 5, Height - 5);

        TextRenderer.DrawText(
            e.Graphics,
            "i",
            AppTheme.SemiBold(10f),
            ClientRectangle,
            AppTheme.Muted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}
