using System.Drawing.Drawing2D;
using NAudio.Wave;

namespace HGmusic;

public class WaveformTrimForm : Form
{
    private readonly string _filePath;
    private readonly double _totalDuration;
    private readonly float[] _peaks;

    private double _startFraction;
    private double _endFraction;
    private bool _draggingStart;
    private bool _draggingEnd;
    private int _lastMouseX;

    private WaveformPanel _waveformPanel = null!;
    private Label _lblFileName = null!;
    private Label _lblDuration = null!;
    private Label _lblStartTime = null!;
    private Label _lblEndTime = null!;
    private Label _lblRangeDuration = null!;
    private Button _btnPreview = null!;
    private Button _btnConfirm = null!;
    private Button _btnCancel = null!;
    private Button _btnReset = null!;

    private WaveOutEvent? _previewOutput;
    private AudioFileReader? _previewReader;
    private System.Windows.Forms.Timer? _previewTimer;

    public double TrimStartSeconds { get; private set; }
    public double TrimEndSeconds { get; private set; }
    public bool Confirmed { get; private set; }

    public WaveformTrimForm(string filePath, double currentTrimStart, double currentTrimEnd, double totalDuration)
    {
        _filePath = filePath;
        _totalDuration = totalDuration > 0 ? totalDuration : WaveformRenderer.GetDuration(filePath);
        _peaks = WaveformRenderer.ExtractPeaks(filePath, 800);

        _startFraction = _totalDuration > 0
            ? Math.Clamp(currentTrimStart / _totalDuration, 0, 1)
            : 0;

        _endFraction = _totalDuration > 0 && currentTrimEnd > 0
            ? Math.Clamp(currentTrimEnd / _totalDuration, _startFraction, 1)
            : 1;

        DoubleBuffered = true;
        Text = "波形裁剪";
        Size = new Size(700, 500);
        MinimumSize = new Size(500, 400);
        BackColor = Theme.Colors.Bg;
        ForeColor = Theme.Colors.TextPrimary;
        Font = Theme.Fonts.Body(10f / 11f);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;

        FormClosed += (_, _) => StopPreview();
        BuildUI();
        UpdateTimeLabels();
    }

    public WaveformTrimForm(string filePath, double currentTrimStart, double currentTrimEnd, double totalDuration, int localDeviceNumber, Action onPreview)
        : this(filePath, currentTrimStart, currentTrimEnd, totalDuration)
    {
        // localDeviceNumber and onPreview are available for future preview functionality
    }

    private void BuildUI()
    {
        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 50,
            BackColor = Theme.Colors.Surface,
        };
        headerPanel.Paint += (_, e) =>
        {
            using var pen = new Pen(Theme.Colors.Border);
            e.Graphics.DrawLine(pen, 0, headerPanel.Height - 1, headerPanel.Width, headerPanel.Height - 1);
        };
        Controls.Add(headerPanel);

        _lblFileName = new Label
        {
            Text = Path.GetFileName(_filePath),
            Location = new Point(16, 6),
            AutoSize = true,
            ForeColor = Theme.Colors.TextPrimary,
            BackColor = Theme.Colors.Surface,
            Font = Theme.Fonts.BodyBold(1f),
        };
        headerPanel.Controls.Add(_lblFileName);

        _lblDuration = new Label
        {
            Text = FormatDuration(_totalDuration),
            Location = new Point(16, 28),
            AutoSize = true,
            ForeColor = Theme.Colors.TextMuted,
            BackColor = Theme.Colors.Surface,
            Font = Theme.Fonts.Caption(9f / 9.5f),
        };
        headerPanel.Controls.Add(_lblDuration);

        _waveformPanel = new WaveformPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Colors.Bg,
            Cursor = Cursors.Cross,
        };
        _waveformPanel.SetWaveformData(_peaks, _startFraction, _endFraction);
        _waveformPanel.MouseDown += WaveformPanel_MouseDown;
        _waveformPanel.MouseMove += WaveformPanel_MouseMove;
        _waveformPanel.MouseUp += WaveformPanel_MouseUp;
        _waveformPanel.MouseLeave += (_, _) =>
        {
            _draggingStart = false;
            _draggingEnd = false;
        };
        Controls.Add(_waveformPanel);

        var bottomPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 100,
            BackColor = Theme.Colors.Surface,
        };
        bottomPanel.Paint += (_, e) =>
        {
            using var pen = new Pen(Theme.Colors.Border);
            e.Graphics.DrawLine(pen, 0, 0, bottomPanel.Width, 0);
        };
        Controls.Add(bottomPanel);

        _lblStartTime = new Label
        {
            Location = new Point(20, 12),
            AutoSize = true,
            ForeColor = Theme.Colors.Success,
            BackColor = Theme.Colors.Surface,
            Font = Theme.Fonts.Caption(1f),
        };
        bottomPanel.Controls.Add(_lblStartTime);

        _lblEndTime = new Label
        {
            Location = new Point(220, 12),
            AutoSize = true,
            ForeColor = Theme.Colors.Error,
            BackColor = Theme.Colors.Surface,
            Font = Theme.Fonts.Caption(1f),
        };
        bottomPanel.Controls.Add(_lblEndTime);

        _lblRangeDuration = new Label
        {
            Location = new Point(420, 12),
            AutoSize = true,
            ForeColor = Theme.Colors.TextMuted,
            BackColor = Theme.Colors.Surface,
            Font = Theme.Fonts.Caption(1f),
        };
        bottomPanel.Controls.Add(_lblRangeDuration);

        _btnPreview = MakeButton(Theme.Icons.Play + "  试听选区", 20, 44, 110, 36, Theme.Colors.Accent);
        _btnPreview.Click += (_, _) => PreviewSelection();
        bottomPanel.Controls.Add(_btnPreview);

        _btnConfirm = MakeButton(Theme.Icons.Check + "  确定", 150, 44, 100, 36, Theme.Colors.Success);
        _btnConfirm.Click += (_, _) =>
        {
            StopPreview();
            TrimStartSeconds = _startFraction * _totalDuration;
            TrimEndSeconds = _endFraction >= 0.999 ? 0 : _endFraction * _totalDuration;
            Confirmed = true;
            DialogResult = DialogResult.OK;
            Close();
        };
        bottomPanel.Controls.Add(_btnConfirm);

        _btnCancel = MakeButton(Theme.Icons.Cross + "  取消", 265, 44, 100, 36, Theme.Colors.TextMuted);
        _btnCancel.Click += (_, _) =>
        {
            StopPreview();
            Confirmed = false;
            DialogResult = DialogResult.Cancel;
            Close();
        };
        bottomPanel.Controls.Add(_btnCancel);

        _btnReset = MakeButton("\u21BA  重置", 380, 44, 100, 36, Theme.Colors.Error);
        _btnReset.Click += (_, _) =>
        {
            _startFraction = 0;
            _endFraction = 1;
            _waveformPanel.SetWaveformData(_peaks, _startFraction, _endFraction);
            UpdateTimeLabels();
        };
        bottomPanel.Controls.Add(_btnReset);
    }

    private void WaveformPanel_MouseDown(object? sender, MouseEventArgs e)
    {
        if (_waveformPanel.Width <= 0) return;

        int startX = (int)(_startFraction * _waveformPanel.Width);
        int endX = (int)(_endFraction * _waveformPanel.Width);

        const int handleRange = 8;

        if (Math.Abs(e.X - startX) <= handleRange)
        {
            _draggingStart = true;
            _draggingEnd = false;
            _lastMouseX = e.X;
        }
        else if (Math.Abs(e.X - endX) <= handleRange)
        {
            _draggingEnd = true;
            _draggingStart = false;
            _lastMouseX = e.X;
        }
    }

    private void WaveformPanel_MouseMove(object? sender, MouseEventArgs e)
    {
        if (!_draggingStart && !_draggingEnd)
        {
            int startX = (int)(_startFraction * _waveformPanel.Width);
            int endX = (int)(_endFraction * _waveformPanel.Width);
            const int handleRange = 8;
            if (Math.Abs(e.X - startX) <= handleRange || Math.Abs(e.X - endX) <= handleRange)
                _waveformPanel.Cursor = Cursors.SizeWE;
            else
                _waveformPanel.Cursor = Cursors.Cross;
            return;
        }

        int panelWidth = _waveformPanel.Width;
        if (panelWidth <= 0) return;

        double newFraction = Math.Clamp((double)e.X / panelWidth, 0, 1);
        int newX = Math.Clamp(e.X, 0, panelWidth);

        if (_draggingStart)
        {
            int endPixelX = (int)(_endFraction * panelWidth);
            if (newX >= endPixelX - 2)
                newX = endPixelX - 2;
            _startFraction = Math.Clamp((double)newX / panelWidth, 0, _endFraction - 0.001);
        }
        else if (_draggingEnd)
        {
            int startPixelX = (int)(_startFraction * panelWidth);
            if (newX <= startPixelX + 2)
                newX = startPixelX + 2;
            _endFraction = Math.Clamp((double)newX / panelWidth, _startFraction + 0.001, 1);
        }

        _waveformPanel.SetWaveformData(_peaks, _startFraction, _endFraction);
        UpdateTimeLabels();
        _lastMouseX = e.X;
    }

    private void WaveformPanel_MouseUp(object? sender, MouseEventArgs e)
    {
        _draggingStart = false;
        _draggingEnd = false;
    }

    private void UpdateTimeLabels()
    {
        double startSec = _startFraction * _totalDuration;
        double endSec = _endFraction * _totalDuration;
        double rangeSec = endSec - startSec;

        _lblStartTime.Text = $"起始: {startSec:F2} 秒";
        _lblEndTime.Text = $"结束: {endSec:F2} 秒";
        _lblRangeDuration.Text = $"时长: {rangeSec:F2} 秒";
    }

    private void PreviewSelection()
    {
        StopPreview();

        try
        {
            double startSec = _startFraction * _totalDuration;
            double endSec = _endFraction * _totalDuration;
            double previewDuration = endSec - startSec;

            if (previewDuration <= 0)
                return;

            _previewReader = new AudioFileReader(_filePath);
            _previewReader.CurrentTime = TimeSpan.FromSeconds(startSec);

            _previewOutput = new WaveOutEvent();
            _previewOutput.Init(_previewReader);
            _previewOutput.Play();

            _previewTimer = new System.Windows.Forms.Timer { Interval = (int)(previewDuration * 1000) + 200 };
            _previewTimer.Tick += (_, _) => StopPreview();
            _previewTimer.Start();
        }
        catch
        {
            StopPreview();
        }
    }

    private void StopPreview()
    {
        _previewTimer?.Stop();
        _previewTimer?.Dispose();
        _previewTimer = null;

        try { _previewOutput?.Stop(); } catch { }
        _previewOutput?.Dispose();
        _previewOutput = null;

        _previewReader?.Dispose();
        _previewReader = null;
    }

    private static Button MakeButton(string text, int x, int y, int w, int h, Color accent)
    {
        var btn = new Button
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(w, h),
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.Colors.ButtonDefault,
            ForeColor = accent,
            Cursor = Cursors.Hand,
            Font = Theme.Fonts.BodyBold(9.5f / 11f),
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.FlatAppearance.MouseOverBackColor = Theme.Colors.ButtonHover;
        return btn;
    }

    private static string FormatDuration(double totalSeconds)
    {
        if (totalSeconds <= 0) return "总时长: --:--";
        var ts = TimeSpan.FromSeconds(totalSeconds);
        return $"总时长: {(int)ts.TotalMinutes}:{ts.Seconds:D2}.{ts.Milliseconds / 10:D2}  ({totalSeconds:F1} 秒)";
    }
}

internal class WaveformPanel : Panel
{
    private float[] _peaks = Array.Empty<float>();
    private double _startFraction;
    private double _endFraction;

    public WaveformPanel()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
    }

    public void SetWaveformData(float[] peaks, double startFraction, double endFraction)
    {
        _peaks = peaks;
        _startFraction = startFraction;
        _endFraction = endFraction;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var g = e.Graphics;

        if (_peaks == null || _peaks.Length == 0)
        {
            g.Clear(Theme.Colors.Bg);
            return;
        }

        var bmp = WaveformRenderer.Render(_peaks, Width, Height, _startFraction, _endFraction);
        g.DrawImage(bmp, 0, 0);
        bmp.Dispose();

        if (Width <= 0) return;

        int startX = (int)(_startFraction * Width);
        int endX = (int)(_endFraction * Width);

        DrawHandle(g, startX, Theme.Colors.Success);
        DrawHandle(g, endX, Theme.Colors.Error);
    }

    private static void DrawHandle(Graphics g, int x, Color color)
    {
        int triHeight = 8;
        int triHalfWidth = 5;

        var tri = new PointF[]
        {
            new PointF(x - triHalfWidth, 0),
            new PointF(x + triHalfWidth, 0),
            new PointF(x, triHeight),
        };

        using var brush = new SolidBrush(color);
        g.FillPolygon(brush, tri);

        using var pen = new Pen(color, 2f);
        g.SmoothingMode = SmoothingMode.None;
        g.DrawLine(pen, x, triHeight, x, g.VisibleClipBounds.Height);
    }
}
