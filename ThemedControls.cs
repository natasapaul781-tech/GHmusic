using System.Drawing.Drawing2D;

namespace HGmusic;

public class ThemedTrackBar : TrackBar
{
    private bool _hovered;
    private bool _dragging;

    public ThemedTrackBar()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        TickStyle = TickStyle.None;
    }

    protected override void OnMouseEnter(EventArgs e) { _hovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hovered = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _dragging = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _dragging = false; Invalidate(); base.OnMouseUp(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        int trackH = 8;
        int trackY = (Height - trackH) / 2;
        var trackRect = new Rectangle(10, trackY, Width - 20, trackH);
        int rad = trackH / 2;

        using var trackPath = RoundedRect(trackRect, rad);
        using var trackBrush = new SolidBrush(Theme.Colors.TrackBarTrack);
        g.FillPath(trackBrush, trackPath);

        float ratio = Maximum > Minimum
            ? (float)(Value - Minimum) / (Maximum - Minimum)
            : 0f;
        int thumbR = _hovered || _dragging ? 11 : 9;
        int thumbX = 10 + (int)((Width - 20 - thumbR * 2) * ratio);
        int thumbY = (Height - thumbR * 2) / 2;
        var thumbRect = new Rectangle(thumbX, thumbY, thumbR * 2, thumbR * 2);

        var thumbColor = _dragging ? Theme.Colors.AccentHover
            : _hovered ? Color.FromArgb(110, 158, 245) : Theme.Colors.Accent;
        using var thumbBrush = new SolidBrush(thumbColor);
        using var thumbShadow = new SolidBrush(Color.FromArgb(30, 0, 0, 0));
        g.FillEllipse(thumbShadow, thumbX + 1, thumbY + 1, thumbR * 2, thumbR * 2);
        g.FillEllipse(thumbBrush, thumbRect);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        using var brush = new SolidBrush(Parent?.BackColor ?? Theme.Colors.Surface);
        e.Graphics.FillRectangle(brush, ClientRectangle);
    }

    private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

public class ThemedComboBox : ComboBox
{
    public ThemedComboBox()
    {
        DrawMode = DrawMode.OwnerDrawFixed;
        DropDownStyle = ComboBoxStyle.DropDownList;
        FlatStyle = FlatStyle.Flat;
        BackColor = Theme.Colors.Surface;
        ForeColor = Theme.Colors.TextPrimary;
        DrawItem += ThemedComboBox_DrawItem;
    }

    private void ThemedComboBox_DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        bool selected = (e.State & DrawItemState.Selected) != 0 || (e.State & DrawItemState.HotLight) != 0;

        using var bgBrush = new SolidBrush(selected ? Theme.Colors.ButtonHover : Theme.Colors.Surface);
        g.FillRectangle(bgBrush, e.Bounds);

        var text = Items[e.Index]?.ToString() ?? "";
        using var textBrush = new SolidBrush(selected ? Theme.Colors.TextPrimary : Theme.Colors.TextSecondary);
        var textRect = new Rectangle(e.Bounds.X + 12, e.Bounds.Y, e.Bounds.Width - 24, e.Bounds.Height);
        var format = new StringFormat { LineAlignment = StringAlignment.Center };
        using var font = Theme.Fonts.Body(1f);
        g.DrawString(text, font, textBrush, textRect, format);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        var g = e.Graphics;
        using var bgBrush = new SolidBrush(Theme.Colors.Surface);
        g.FillRectangle(bgBrush, ClientRectangle);
    }
}

public class ThemedScrollBar : NativeWindow, IDisposable
{
    public static void ApplyToControl(Control control)
    {
        control.HandleCreated += (_, _) =>
        {
            _ = new ThemedScrollBar(control.Handle);
        };
    }

    private readonly IntPtr _handle;
    private bool _disposed;

    private ThemedScrollBar(IntPtr handle)
    {
        _handle = handle;
        AssignHandle(handle);
    }

    protected override void WndProc(ref Message m)
    {
        const int WM_NCPAINT = 0x0085;
        const int WM_HSCROLL = 0x0114;
        const int WM_VSCROLL = 0x0115;

        if (m.Msg == WM_NCPAINT)
        {
            base.WndProc(ref m);
            DrawScrollBars();
            return;
        }

        base.WndProc(ref m);

        if (m.Msg == WM_HSCROLL || m.Msg == WM_VSCROLL)
        {
            DrawScrollBars();
        }
    }

    private void DrawScrollBars()
    {
        if (!NativeMethods.IsWindowVisible(_handle)) return;

        var rect = new NativeMethods.RECT();
        NativeMethods.GetClientRect(_handle, ref rect);

        if (!NativeMethods.GetScrollInfo(_handle, NativeMethods.SB_VERT, out var si)) return;

        int scrollW = SystemInformation.VerticalScrollBarWidth;

        using var g = Graphics.FromHwnd(_handle);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using var trackBrush = new SolidBrush(Theme.Colors.ScrollBarTrack);
        g.FillRectangle(trackBrush, rect.Right - scrollW, 0, scrollW, rect.Bottom);

        if (si.nMax > 0 && si.nPage < (uint)(si.nMax - si.nMin + 1))
        {
            int totalRange = si.nMax - si.nMin;
            float ratio = si.nPage / (float)totalRange;
            int thumbH = Math.Max(24, (int)(rect.Bottom * ratio));
            float posRatio = si.nPos / (float)(totalRange - si.nPage + 1);
            int thumbY = (int)((rect.Bottom - thumbH) * posRatio);

            int thumbW = scrollW - 4;
            int thumbX = rect.Right - scrollW + 2;

            var thumbRect = new Rectangle(thumbX, thumbY, thumbW, thumbH);
            using var thumbPath = RoundedRect(thumbRect, 5);
            using var thumbBrush = new SolidBrush(Theme.Colors.ScrollBarThumb);
            g.FillPath(thumbBrush, thumbPath);
        }
    }

    private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            ReleaseHandle();
            _disposed = true;
        }
    }

    private static class NativeMethods
    {
        public const int SB_VERT = 1;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool GetClientRect(IntPtr hWnd, ref RECT lpRect);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        public static extern bool GetScrollInfo(IntPtr hwnd, int nBar, out SCROLLINFO lpsi);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct SCROLLINFO
        {
            public uint cbSize;
            public uint fMask;
            public int nMin;
            public int nMax;
            public uint nPage;
            public int nPos;
            public int nTrackPos;

            public static SCROLLINFO Create()
            {
                return new SCROLLINFO
                {
                    cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<SCROLLINFO>(),
                    fMask = 0x17
                };
            }
        }
    }
}

public class ThemedCheckBox : CheckBox
{
    public ThemedCheckBox()
    {
        FlatStyle = FlatStyle.Flat;
        AutoSize = false;
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var parentBg = Parent?.BackColor ?? Theme.Colors.Surface;
        if (parentBg.A == 0) parentBg = Theme.Colors.Surface;
        using var bgBrush = new SolidBrush(parentBg);
        g.FillRectangle(bgBrush, ClientRectangle);

        int boxSize = 18;
        int boxY = (Height - boxSize) / 2;

        using var boxPath = RoundedRect(0, boxY, boxSize, boxSize, 4);
        using var boxPen = new Pen(Checked ? Theme.Colors.Accent : Theme.Colors.Border, 2f);
        g.DrawPath(boxPen, boxPath);

        if (Checked)
        {
            using var fillBrush = new SolidBrush(Theme.Colors.AccentDim);
            g.FillPath(fillBrush, boxPath);

            using var checkFont = new Font("Segoe UI", 10f, FontStyle.Bold);
            using var checkBrush = new SolidBrush(Theme.Colors.Accent);
            g.DrawString(Theme.Icons.Check, checkFont, checkBrush, 2, boxY - 1);
        }

        using var textBrush = new SolidBrush(Enabled ? Theme.Colors.TextPrimary : Theme.Colors.TextMuted);
        using var textFont = Theme.Fonts.Body(1f);
        var textRect = new Rectangle(boxSize + 8, 0, Width - boxSize - 8, Height);
        var format = new StringFormat { LineAlignment = StringAlignment.Center };
        g.DrawString(Text, textFont, textBrush, textRect, format);
    }

    private static GraphicsPath RoundedRect(int x, int y, int w, int h, int r)
    {
        var path = new GraphicsPath();
        int d = r * 2;
        path.AddArc(x, y, d, d, 180, 90);
        path.AddArc(x + w - d, y, d, d, 270, 90);
        path.AddArc(x + w - d, y + h - d, d, d, 0, 90);
        path.AddArc(x, y + h - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
