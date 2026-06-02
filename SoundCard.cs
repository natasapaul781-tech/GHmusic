using System.Drawing.Drawing2D;

namespace Soundboard;

public class SoundCard : Control
{
    private const float BaseW = 190f;
    private const float BaseH = 210f;
    private const int BaseRad = 18;
    private const float IconFontSz = 26f;
    private const float IconY = 34f;
    private const float NameFontSz = 11.5f;
    private const float NameY = 74f;
    private const float KeyFontSz = 9.5f;
    private const float KeyY = 108f;
    private const float DurFontSz = 9f;
    private const float DurY = 132f;
    private const float TrimFontSz = 8.5f;
    private const float TrimY = 154f;
    private const float StatusFontSz = 9f;
    private const float StatusY = 178f;
    private const float DotSize = 8f;
    private const float DotX = 16f;
    private const float DotY = 20f;
    private const float PlusFontSz = 38f;
    private const float AddLabelFontSz = 11f;
    private const float BorderWid = 1.5f;
    private const float GlowWid = 14f;

    private bool _hovered;
    private bool _pressed;
    private float _animProgress = 1f;
    private float _playingPulse;
    private readonly System.Windows.Forms.Timer _animTimer;

    public static float UIScale { get; set; } = 1.25f;

    public HotkeyBinding? Binding { get; set; }
    public bool IsAddCard { get; set; }
    public bool IsSelected { get; set; }

    public event EventHandler? CardClicked;
    public event EventHandler? CardRightClicked;

    public SoundCard()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        DoubleBuffered = true;
        Size = new Size((int)(BaseW * UIScale), (int)(BaseH * UIScale));

        _animTimer = new System.Windows.Forms.Timer { Interval = 16 };
        _animTimer.Tick += (_, _) =>
        {
            if (IsDisposed) return;
            bool isPlaying = Binding?.IsPlaying == true;
            if (!_hovered && !isPlaying
                && Math.Abs(_animProgress - 1f) < 0.01f && Math.Abs(_playingPulse) < 0.01f)
                return;
            _animProgress += (1f - _animProgress) * 0.18f;
            if (isPlaying)
                _playingPulse = (float)(0.40 + 0.60 * Math.Sin(DateTime.Now.Ticks / 2500000.0));
            else
                _playingPulse += (0f - _playingPulse) * 0.06f;
            Invalidate();
        };
        _animTimer.Start();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _animTimer.Stop(); _animTimer.Dispose(); }
        base.Dispose(disposing);
    }

    protected override void OnMouseEnter(EventArgs e) { _hovered = true; _animProgress = 0f; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hovered = false; _pressed = false; _animProgress = 0f; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { _pressed = true; Invalidate(); } base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (_pressed && e.Button == MouseButtons.Left) { _pressed = false; Invalidate(); CardClicked?.Invoke(this, EventArgs.Empty); }
        if (e.Button == MouseButtons.Right) CardRightClicked?.Invoke(this, EventArgs.Empty);
        base.OnMouseUp(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (IsDisposed) return;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        if (IsAddCard) { PaintAddCard(g); return; }
        PaintCard(g);
    }

    private void PaintCard(Graphics g)
    {
        float s = UIScale;
        var r = new RectangleF(2 * s, 2 * s, Width - 4 * s, Height - 4 * s);
        int rad = (int)(BaseRad * s);
        using var path = RoundedRect(r, rad);

        bool isPlaying = Binding?.IsPlaying == true;

        if (isPlaying)
        {
            using var glow = new Pen(Color.FromArgb(30, Theme.Colors.CardPlayingDot), GlowWid * s);
            g.DrawPath(glow, path);
        }
        else if (_hovered)
        {
            var eased = Theme.Easing.EaseOut(Math.Clamp(_animProgress, 0f, 1f));
            var a = (int)(30 + 20 * eased);
            using var glow = new Pen(Color.FromArgb(a, 100, 140, 220), GlowWid * s);
            g.DrawPath(glow, path);
        }

        Color top, bot;
        if (_pressed) { top = Color.FromArgb(26, 27, 38); bot = Color.FromArgb(20, 21, 30); }
        else
        {
            top = Lerp(Theme.Colors.CardBgTop, Theme.Colors.CardHoverTop, _hovered ? _animProgress : 0);
            bot = Lerp(Theme.Colors.CardBgBot, Theme.Colors.CardHoverBot, _hovered ? _animProgress : 0);
        }
        using var bgBrush = new LinearGradientBrush(r, top, bot, 90f);
        g.FillPath(bgBrush, path);

        using var innerR = RoundedRect(new RectangleF(r.X + 2 * s, r.Y + 2 * s, r.Width - 4 * s, 5 * s), rad / 2);
        using var innerBrush = new LinearGradientBrush(innerR.GetBounds(), Color.FromArgb(35, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90f);
        g.FillPath(innerBrush, innerR);

        Color borderC;
        if (isPlaying) borderC = Theme.Colors.CardPlayingBorder;
        else if (IsSelected) borderC = Color.FromArgb(140, 175, 250);
        else borderC = Lerp(Theme.Colors.CardBorder, Theme.Colors.CardBorderHover, _hovered ? _animProgress : 0);
        var borderW = IsSelected ? BorderWid * s * 1.5f : BorderWid * s;
        using var borderPen = new Pen(borderC, borderW);
        g.DrawPath(borderPen, path);

        if (Binding == null) return;

        if (isPlaying)
        {
            var alpha = (int)(180 + 75 * _playingPulse);
            using var dotBrush = new SolidBrush(Color.FromArgb(alpha, Theme.Colors.CardPlayingDot));
            g.FillEllipse(dotBrush, DotX * s, DotY * s, DotSize * s, DotSize * s);
        }

        var ext = Path.GetExtension(Binding.FilePath).ToLowerInvariant();
        var icon = ext switch { ".mp3" => Theme.Icons.Music, ".wav" => Theme.Icons.Wave, _ => Theme.Icons.FileAudio };
        var iconClr = isPlaying ? Theme.Colors.CardPlayingDot : Theme.Colors.CardIcon;
        using var iconFont = Theme.Fonts.Title(s);
        var iconSz = g.MeasureString(icon, iconFont);
        using var iconBrush = new SolidBrush(iconClr);
        g.DrawString(icon, iconFont, iconBrush, (Width - iconSz.Width) / 2, IconY * s);

        var name = Binding.FileName;
        var maxWidth = (int)(BaseW * s - 24 * s);
        using var nameFont = Theme.Fonts.BodyBold(s);
        name = TruncateText(g, name, nameFont, maxWidth);
        var nameSz = g.MeasureString(name, nameFont);
        using var nameBrush = new SolidBrush(Theme.Colors.TextPrimary);
        g.DrawString(name, nameFont, nameBrush, (Width - nameSz.Width) / 2, NameY * s);

        var keyText = Binding.Key == (int)Keys.None ? "未绑定" : Binding.KeyDisplay;
        var keyClr = Binding.Key == (int)Keys.None ? Theme.Colors.TextMuted : Theme.Colors.TextSecondary;
        using var keyFont = Theme.Fonts.Caption(s);
        var keyDisplay = TruncateText(g, keyText, keyFont, maxWidth);
        var keySz = g.MeasureString(keyDisplay, keyFont);
        using var keyBrush = new SolidBrush(keyClr);
        g.DrawString(keyDisplay, keyFont, keyBrush, (Width - keySz.Width) / 2, KeyY * s);

        var dur = Binding.DurationDisplay;
        using var durFont = Theme.Fonts.Small(s);
        var durSz = g.MeasureString(dur, durFont);
        using var durBrush = new SolidBrush(Theme.Colors.TextMuted);
        g.DrawString(dur, durFont, durBrush, (Width - durSz.Width) / 2, DurY * s);

        if (Binding.HasTrim)
        {
            using var trimFont = Theme.Fonts.Small(s);
            var trimDisplay = TruncateText(g, Binding.TrimDisplay, trimFont, maxWidth);
            var trimSz = g.MeasureString(trimDisplay, trimFont);
            using var trimBrush = new SolidBrush(Theme.Colors.Warning);
            g.DrawString(trimDisplay, trimFont, trimBrush, (Width - trimSz.Width) / 2, TrimY * s);
        }

        var statusText = isPlaying ? "播放中" : (Binding.Key == (int)Keys.None ? "点击设置" : "");
        if (statusText.Length > 0)
        {
            using var statusFont = Theme.Fonts.Caption(s);
            var statusSz = g.MeasureString(statusText, statusFont);
            using var statusBrush = new SolidBrush(isPlaying ? Theme.Colors.CardPlayingDot : Theme.Colors.TextMuted);
            g.DrawString(statusText, statusFont, statusBrush, (Width - statusSz.Width) / 2, StatusY * s);
        }
    }

    private void PaintAddCard(Graphics g)
    {
        float s = UIScale;
        var r = new RectangleF(2 * s, 2 * s, Width - 4 * s, Height - 4 * s);
        using var path = RoundedRect(r, (int)(BaseRad * s));

        Color top = _hovered ? Color.FromArgb(28, 29, 42) : Theme.Colors.AddCardBgTop;
        Color bot = _hovered ? Color.FromArgb(22, 23, 34) : Theme.Colors.AddCardBgBot;
        using var bgBrush = new LinearGradientBrush(r, top, bot, 90f);
        g.FillPath(bgBrush, path);

        var borderC = _hovered ? Theme.Colors.AddCardBorderHover : Theme.Colors.AddCardBorder;
        using var borderPen = new Pen(borderC, 1.5f * s) { DashStyle = DashStyle.Dash, DashPattern = new float[] { 5 * s, 3 * s } };
        g.DrawPath(borderPen, path);

        using var plusFont = new Font(Theme.Fonts.Family, PlusFontSz * s);
        var plusSz = g.MeasureString(Theme.Icons.Add, plusFont);
        var textClr = _hovered ? Theme.Colors.AddCardTextHover : Theme.Colors.AddCardText;
        using var plusBrush = new SolidBrush(textClr);
        g.DrawString(Theme.Icons.Add, plusFont, plusBrush, (Width - plusSz.Width) / 2, (Height - plusSz.Height) / 2 - 16 * s);

        using var labelFont = new Font(Theme.Fonts.Family, AddLabelFontSz * s);
        var label = "添加音效";
        var labelSz = g.MeasureString(label, labelFont);
        g.DrawString(label, labelFont, plusBrush, (Width - labelSz.Width) / 2, (Height + plusSz.Height) / 2 - 18 * s);
    }

    private static string TruncateText(Graphics g, string text, Font font, int maxWidth)
    {
        var sz = g.MeasureString(text, font);
        if (sz.Width <= maxWidth) return text;
        for (int i = text.Length - 1; i > 0; i--)
        {
            var candidate = text[..i] + "..";
            if (g.MeasureString(candidate, font).Width <= maxWidth)
                return candidate;
        }
        return "..";
    }

    private static GraphicsPath RoundedRect(RectangleF bounds, int radius)
    {
        var path = new GraphicsPath();
        float d = radius * 2;
        float x = bounds.X, y = bounds.Y, w = bounds.Width, h = bounds.Height;
        path.AddArc(x, y, d, d, 180, 90);
        path.AddArc(x + w - d, y, d, d, 270, 90);
        path.AddArc(x + w - d, y + h - d, d, d, 0, 90);
        path.AddArc(x, y + h - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static Color Lerp(Color a, Color b, float t) =>
        Color.FromArgb(
            (int)(a.A + (b.A - a.A) * t),
            (int)(a.R + (b.R - a.R) * t),
            (int)(a.G + (b.G - a.G) * t),
            (int)(a.B + (b.B - a.B) * t));
}
