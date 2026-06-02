namespace Soundboard;

public class AnimatedButton : Button
{
    private Color _normalColor = Theme.Colors.ButtonDefault;
    private Color _hoverColor = Theme.Colors.ButtonHover;
    private Color _currentColor = Theme.Colors.ButtonDefault;
    private Color _targetColor = Theme.Colors.ButtonDefault;
    private float _scale = 1f;
    private float _targetScale = 1f;
    private readonly System.Windows.Forms.Timer _animTimer;
    private float _animProgress;
    private bool _hovered;
    private bool _pressed;

    public Color NormalColor
    {
        get => _normalColor;
        set { _normalColor = value; if (!_hovered && !_pressed) SetTarget(value); }
    }
    public Color HoverColor { get => _hoverColor; set => _hoverColor = value; }

    public AnimatedButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);

        _animTimer = new System.Windows.Forms.Timer { Interval = 16 };
        _animTimer.Tick += AnimTick;
        _animTimer.Start();
    }

    public void SetColors(Color normal, Color hover)
    {
        _normalColor = normal;
        _hoverColor = hover;
        SetTarget(_hovered ? hover : normal);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hovered = true;
        SetTarget(_hoverColor);
        _targetScale = 1.02f;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hovered = false;
        _pressed = false;
        SetTarget(_normalColor);
        _targetScale = 1f;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _pressed = true;
            _targetScale = 0.97f;
            _animTimer.Start();
            Invalidate();
        }
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        _pressed = false;
        _targetScale = _hovered ? 1.02f : 1f;
        Invalidate();
        base.OnMouseUp(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var rect = new Rectangle(0, 0, Width, Height);

        float cx = Width / 2f;
        float cy = Height / 2f;
        g.TranslateTransform(cx, cy);
        g.ScaleTransform(_scale, _scale);
        g.TranslateTransform(-cx, -cy);

        using var bgBrush = new SolidBrush(_currentColor);
        g.FillRectangle(bgBrush, rect);

        using var textBrush = new SolidBrush(ForeColor);
        var textFormat = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
        };
        using var font = new Font(Font, Font.Style);
        g.DrawString(Text, font, textBrush, rect, textFormat);

        g.ResetTransform();
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        var bg = BackColor.A > 0 && BackColor != Color.Transparent ? BackColor
            : Parent?.BackColor ?? Theme.Colors.ButtonDefault;
        if (bg.A == 0) bg = Theme.Colors.ButtonDefault;
        using var brush = new SolidBrush(bg);
        e.Graphics.FillRectangle(brush, ClientRectangle);
    }

    private void AnimTick(object? sender, EventArgs e)
    {
        _animProgress += (1f - _animProgress) * 0.22f;

        var eased = Theme.Easing.EaseOut(Math.Clamp(_animProgress, 0f, 1f));
        _currentColor = LerpColor(_currentColor, _targetColor, eased * 0.5f);
        _scale += (_targetScale - _scale) * 0.25f;

        if (Math.Abs(_scale - _targetScale) < 0.0005f
            && Math.Abs(_currentColor.R - _targetColor.R) < 1
            && Math.Abs(_currentColor.G - _targetColor.G) < 1
            && Math.Abs(_currentColor.B - _targetColor.B) < 1)
        {
            _currentColor = _targetColor;
            _scale = _targetScale;
            _animTimer.Stop();
            return;
        }
        Invalidate();
    }

    private void SetTarget(Color target)
    {
        _targetColor = target;
        _animProgress = 0f;
        _animTimer.Start();
    }

    private static Color LerpColor(Color a, Color b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return Color.FromArgb(
            (int)(a.A + (b.A - a.A) * t),
            (int)(a.R + (b.R - a.R) * t),
            (int)(a.G + (b.G - a.G) * t),
            (int)(a.B + (b.B - a.B) * t));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _animTimer.Stop(); _animTimer.Dispose(); }
        base.Dispose(disposing);
    }
}
