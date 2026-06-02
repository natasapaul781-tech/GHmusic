using System.Drawing.Drawing2D;

namespace HGmusic;

public class CharityDonationForm : Form
{
    private readonly float _uiScale = 1.25f;
    private readonly Image? _loadedImage;

    private static class C
    {
        public static readonly Color BgTop = Color.FromArgb(255, 248, 240);
        public static readonly Color BgBottom = Color.FromArgb(252, 238, 224);

        public static readonly Color CardBorder = Color.FromArgb(235, 215, 195);

        public static readonly Color Accent = Color.FromArgb(235, 115, 100);
        public static readonly Color AccentHover = Color.FromArgb(218, 98, 83);

        public static readonly Color Heart = Color.FromArgb(235, 100, 85);

        public static readonly Color TextTitle = Color.FromArgb(62, 48, 40);
        public static readonly Color TextBody = Color.FromArgb(100, 78, 65);
        public static readonly Color TextSub = Color.FromArgb(160, 135, 115);
        public static readonly Color TextDecor = Color.FromArgb(210, 190, 170);

        public static readonly Color ImageBg = Color.FromArgb(252, 245, 238);

        public static readonly Color CloseNormal = Color.FromArgb(180, 155, 135);
        public static readonly Color CloseHoverBg = Color.FromArgb(248, 235, 225);

        public static readonly Color DividerLine = Color.FromArgb(235, 220, 205);
    }

    public CharityDonationForm()
    {
        _loadedImage = TryLoadImage();
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterScreen;
        TopMost = true;
        BackColor = C.BgBottom;
        MaximizeBox = false;
        MinimizeBox = false;
        DoubleBuffered = true;
        Text = "用爱点亮希望";

        var imgH = CalculateImageHeight();
        var w = Scale(580);
        var padX = Scale(44);
        var contentW = w - padX * 2;

        var heartH = Scale(72);
        var titleH = Scale(46);
        var subH = Scale(36);
        var spacer1 = Scale(28);
        var imgSectionH = Scale(imgH + 36);
        var spacer2 = Scale(24);
        var gapAfterImage = Scale(32);
        var sloganH = Scale(76);
        var spacer4 = Scale(16);
        var thanksH = Scale(34);
        var spacer5 = Scale(28);
        var btnH = Scale(66);
        var spacer6 = Scale(18);
        var sigH = Scale(32);
        var topPad = Scale(32);
        var bottomPad = Scale(36);

        var contentH = topPad + heartH + titleH + subH + spacer1 + imgSectionH + spacer2
            + gapAfterImage + sloganH + spacer4 + thanksH + spacer5 + btnH + spacer6 + sigH + bottomPad;

        var h = contentH;
        Size = new Size(w, h);

        using (var formPath = CreateRoundPath(ClientRectangle, Scale(16)))
        {
            Region = new Region(formPath);
        }

        var root = new DoubleBufferedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent
        };
        root.Paint += OnRootPaint;

        var closeBtn = BuildCloseButton();
        root.Controls.Add(closeBtn);
        closeBtn.BringToFront();

        var y = topPad;

        var heart = BuildCenteredLabel("♥", C.Heart, "Segoe UI Emoji", 36f, contentW, y, heartH);
        root.Controls.Add(heart);
        y += heartH;

        var title = BuildCenteredLabel("谢谢你，善良的人", C.TextTitle, "Microsoft YaHei UI", 22f, contentW, y, titleH, FontStyle.Bold);
        root.Controls.Add(title);
        y += titleH;

        var sub = BuildCenteredLabel("你的每一次关注，都在为一个孩子点亮希望", C.TextSub, "Microsoft YaHei UI", 11f, contentW, y, subH);
        root.Controls.Add(sub);
        y += subH + spacer1;

        var imgPanel = BuildImagePanel(contentW, imgH, padX, y);
        root.Controls.Add(imgPanel);
        y += imgSectionH + spacer2 + gapAfterImage;

        var slogan = BuildCenteredLabel("世界或许不完美\r\n但因为有你，它正在变好", C.TextBody, "Microsoft YaHei UI", 13.5f, contentW, y, sloganH);
        root.Controls.Add(slogan);
        y += sloganH + spacer4;

        var thanks = BuildCenteredLabel("感谢您的每一次驻足", C.TextSub, "Microsoft YaHei UI", 10.5f, contentW, y, thanksH);
        root.Controls.Add(thanks);
        y += thanksH + spacer5;

        var btn = BuildAckButton(contentW, padX, y);
        root.Controls.Add(btn);
        y += btnH + spacer6;

        var sig = BuildCenteredLabel("——  以爱之名，温暖世界  ——", C.TextDecor, "Microsoft YaHei UI", 9.5f, contentW, y, sigH);
        root.Controls.Add(sig);

        Controls.Add(root);

        Opacity = 0;
        Shown += (_, _) => AnimateFadeIn();
    }

    private Button BuildCloseButton()
    {
        var btn = new Button
        {
            Text = "",
            Size = new Size(Scale(38), Scale(38)),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 13f * _uiScale),
            Cursor = Cursors.Hand,
            Location = new Point(Width - Scale(56), Scale(18)),
            TabStop = false
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.FlatAppearance.MouseOverBackColor = C.CloseHoverBg;
        btn.Paint += (_, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = btn.ClientRectangle;
            var hover = r.Contains(btn.PointToClient(Cursor.Position));
            if (hover)
            {
                using var bgBrush = new SolidBrush(C.CloseHoverBg);
                using var path = CreateRoundPath(r, Scale(6));
                g.FillPath(bgBrush, path);
            }
            using var textBrush = new SolidBrush(C.CloseNormal);
            var ts = g.MeasureString("✕", btn.Font);
            g.DrawString("✕", btn.Font, textBrush,
                (r.Width - ts.Width) / 2, (r.Height - ts.Height) / 2);
        };
        btn.MouseEnter += (_, _) => btn.Invalidate();
        btn.MouseLeave += (_, _) => btn.Invalidate();
        btn.Click += (_, _) => AnimateClose();
        return btn;
    }

    private Label BuildCenteredLabel(string text, Color color, string fontFamily,
        float fontSize, int containerW, int y, int height, FontStyle style = FontStyle.Regular)
    {
        return new Label
        {
            Text = text,
            Font = new Font(fontFamily, fontSize * _uiScale, style),
            ForeColor = color,
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleCenter,
            AutoSize = false,
            Size = new Size(containerW, height),
            Location = new Point((Width - containerW) / 2, y)
        };
    }

    private DoubleBufferedPanel BuildImagePanel(int contentW, int imgH, int padX, int y)
    {
        var outerPad = Scale(14);
        var outer = new DoubleBufferedPanel
        {
            Bounds = new Rectangle(padX - outerPad, y - outerPad,
                contentW + outerPad * 2, Scale(imgH + 28)),
            BackColor = Color.Transparent
        };

        outer.Paint += (_, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(outerPad, outerPad, outer.Width - outerPad * 2, outer.Height - outerPad * 2);
            using var path = CreateRoundPath(r, Scale(12));
            DrawSoftShadow(g, path);
        };

        var inner = new DoubleBufferedPanel
        {
            Bounds = new Rectangle(outerPad, outerPad,
                outer.Width - outerPad * 2, outer.Height - outerPad * 2),
            BackColor = C.ImageBg
        };
        inner.Paint += (_, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = inner.ClientRectangle;
            using var path = CreateRoundPath(r, Scale(12));
            using var brush = new SolidBrush(C.ImageBg);
            g.FillPath(brush, path);
            using var pen = new Pen(C.ImageBg, 1);
            g.DrawPath(pen, path);
        };

        var picPadding = Scale(4);
        var pic = new PictureBox
        {
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent,
            Bounds = new Rectangle(picPadding, picPadding,
                inner.Width - picPadding * 2, inner.Height - picPadding * 2),
            Image = _loadedImage
        };
        inner.Controls.Add(pic);
        outer.Controls.Add(inner);
        return outer;
    }

    private DoubleBufferedPanel BuildDivider(int contentW, int padX, int y)
    {
        var p = new DoubleBufferedPanel
        {
            Bounds = new Rectangle(padX + Scale(30), y, contentW - Scale(60), Scale(20)),
            BackColor = Color.Transparent
        };
        p.Paint += (_, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = p.ClientRectangle;
            var cy = r.Height / 2;
            using var pen = new Pen(C.DividerLine, 1);
            g.DrawLine(pen, 0, cy, r.Width, cy);
            var mid = r.Width / 2;
            var dotR = Scale(4);
            using var dotBrush = new SolidBrush(C.TextDecor);
            g.FillEllipse(dotBrush, mid - dotR, cy - dotR, dotR * 2, dotR * 2);
        };
        return p;
    }

    private Button BuildAckButton(int contentW, int padX, int y)
    {
        var btnW = Scale(280);
        var btn = new Button
        {
            Text = "",
            Size = new Size(btnW, Scale(52)),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            TabStop = false,
            Location = new Point((Width - btnW) / 2, y + Scale(7))
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.FlatAppearance.MouseOverBackColor = C.AccentHover;
        btn.FlatAppearance.MouseDownBackColor = C.Accent;
        btn.Paint += (_, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(0, 0, btn.Width - 1, btn.Height - 1);

            var hover = r.Contains(btn.PointToClient(Cursor.Position));
            var back = hover ? C.AccentHover : C.Accent;
            using var path = CreateRoundPath(r, Scale(26));
            using var brush = new SolidBrush(back);
            g.FillPath(brush, path);

            using var glowBrush = new SolidBrush(Color.FromArgb(20, C.Accent));
            var glowRect = new Rectangle(r.X, r.Y + Scale(5), r.Width, r.Height);
            using var glowPath = CreateRoundPath(glowRect, Scale(26));
            g.FillPath(glowBrush, glowPath);

            var text = "我知道了  ♥";
            using var font = new Font("Microsoft YaHei UI", 15f * _uiScale, FontStyle.Bold);
            var ts = g.MeasureString(text, font);
            g.DrawString(text, font, Brushes.White,
                (r.Width - ts.Width) / 2, (r.Height - ts.Height) / 2);
        };
        btn.MouseEnter += (_, _) => btn.Invalidate();
        btn.MouseLeave += (_, _) => btn.Invalidate();
        btn.Click += (_, _) => AnimateClose();
        return btn;
    }

    private void OnRootPaint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = ClientRectangle;

        using var bgBrush = new LinearGradientBrush(rect, C.BgTop, C.BgBottom, LinearGradientMode.Vertical);
        g.FillRectangle(bgBrush, rect);

        using var clipPath = CreateRoundPath(rect, Scale(16));
        g.SetClip(clipPath);

        var r1 = ScaleRect(-50, -30, 280, 280);
        using var b1 = new LinearGradientBrush(r1,
            Color.FromArgb(50, 255, 210, 190), Color.Transparent, LinearGradientMode.ForwardDiagonal);
        using var p1 = new GraphicsPath(); p1.AddEllipse(r1);
        g.FillPath(b1, p1);

        var r2 = ScaleRect(rect.Width - 240, rect.Height - 240, 250, 250);
        using var b2 = new LinearGradientBrush(r2,
            Color.Transparent, Color.FromArgb(40, 255, 200, 175), LinearGradientMode.ForwardDiagonal);
        using var p2 = new GraphicsPath(); p2.AddEllipse(r2);
        g.FillPath(b2, p2);

        var r3 = ScaleRect(rect.Width - 150, (int)(rect.Height * 0.55f), 140, 140);
        using var b3 = new SolidBrush(Color.FromArgb(18, 235, 115, 100));
        using var p3 = new GraphicsPath(); p3.AddEllipse(r3);
        g.FillPath(b3, p3);

        g.ResetClip();

        using var pen = new Pen(C.CardBorder, 1);
        using var outerPath = CreateRoundPath(
            new Rectangle(0, 0, rect.Width - 1, rect.Height - 1), Scale(16));
        g.DrawPath(pen, outerPath);
    }

    private static void DrawSoftShadow(Graphics g, GraphicsPath path)
    {
        for (var i = 6; i >= 1; i--)
        {
            var alpha = 8 - i;
            using var shadowBrush = new SolidBrush(Color.FromArgb(alpha, 0, 0, 0));
            g.TranslateTransform(0, i * 0.6f);
            g.FillPath(shadowBrush, path);
            g.ResetTransform();
        }
    }

    private Rectangle ScaleRect(int x, int y, int w, int h)
    {
        return new Rectangle(Scale(x), Scale(y), Scale(w), Scale(h));
    }

    private int Scale(int value)
    {
        return (int)(value * _uiScale);
    }

    private void AnimateFadeIn()
    {
        var timer = new System.Windows.Forms.Timer { Interval = 16 };
        var step = 0;
        timer.Tick += (_, _) =>
        {
            step++;
            Opacity = Math.Min(1.0, step * 0.07);
            if (Opacity >= 1.0)
            {
                timer.Stop();
                timer.Dispose();
            }
        };
        timer.Start();
    }

    private void AnimateClose()
    {
        var timer = new System.Windows.Forms.Timer { Interval = 16 };
        timer.Tick += (_, _) =>
        {
            Opacity -= 0.10;
            if (Opacity <= 0)
            {
                timer.Stop();
                timer.Dispose();
                Close();
            }
        };
        timer.Start();
    }

    private static Image? TryLoadImage()
    {
        try
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "image", "xiaohonghua.jpg");
            if (!File.Exists(path)) return null;
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return Image.FromStream(fs);
        }
        catch { return null; }
    }

    private int CalculateImageHeight()
    {
        const int containerWidth = 400;
        if (_loadedImage is null) return (int)(containerWidth * 0.6);
        var ratio = (float)_loadedImage.Height / _loadedImage.Width;
        return Math.Clamp((int)(containerWidth * ratio), 120, 350);
    }

    private static GraphicsPath CreateRoundPath(Rectangle rect, int radius)
    {
        var d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left)
        {
            NativeMethods.ReleaseCapture();
            NativeMethods.SendMessage(Handle, 0x00A1, 0x2, IntPtr.Zero);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _loadedImage?.Dispose();
        base.Dispose(disposing);
    }

    private static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool ReleaseCapture();
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    }
}
