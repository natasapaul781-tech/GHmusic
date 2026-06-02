using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net.Http;
using NAudio.Wave;

namespace HGmusic;

public class VBCableInstallForm : Form
{
    private readonly Label _lblTitle;
    private readonly Label _lblStep1, _lblStep2, _lblStep3, _lblStep4;
    private readonly Label _lblStatus;
    private readonly ProgressBar _progressBar;
    private readonly Label _lblPercent;
    private readonly Label _lblStep1Text, _lblStep2Text, _lblStep3Text, _lblStep4Text;
    private Button _btnCancel;
    private readonly Label _lblDetails;
    private bool _cancelled;

    public bool Success { get; private set; }
    public string ResultMessage { get; private set; } = "";

    public VBCableInstallForm()
    {
        Text = "安装 VB-Audio Virtual Cable";
        Size = new Size(500, 380);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Theme.Colors.Surface;
        ForeColor = Theme.Colors.TextPrimary;

        _lblTitle = new Label
        {
            Text = "VB-Audio Virtual Cable 安装向导",
            Location = new Point(30, 25),
            Size = new Size(440, 30),
            Font = Theme.Fonts.Title(14f / 16f),
            ForeColor = Theme.Colors.TextPrimary,
            BackColor = Theme.Colors.Surface,
            TextAlign = ContentAlignment.MiddleCenter,
        };
        Controls.Add(_lblTitle);

        int stepY = 80;
        int indicatorX = 40;

        _lblStep1 = new Label { Text = Theme.Icons.DotInactive, Location = new Point(indicatorX, stepY), Size = new Size(20, 20), ForeColor = Theme.Colors.TextMuted, Font = Theme.Fonts.Body(14f / 11f), BackColor = Theme.Colors.Surface };
        _lblStep1Text = CreateStepLabel("步骤 1：下载安装包", indicatorX + 35, stepY + 5);
        Controls.Add(_lblStep1);

        stepY += 45;
        _lblStep2 = new Label { Text = Theme.Icons.DotInactive, Location = new Point(indicatorX, stepY), Size = new Size(20, 20), ForeColor = Theme.Colors.TextMuted, Font = Theme.Fonts.Body(14f / 11f), BackColor = Theme.Colors.Surface };
        _lblStep2Text = CreateStepLabel("步骤 2：解压文件", indicatorX + 35, stepY + 5);
        Controls.Add(_lblStep2);

        stepY += 45;
        _lblStep3 = new Label { Text = Theme.Icons.DotInactive, Location = new Point(indicatorX, stepY), Size = new Size(20, 20), ForeColor = Theme.Colors.TextMuted, Font = Theme.Fonts.Body(14f / 11f), BackColor = Theme.Colors.Surface };
        _lblStep3Text = CreateStepLabel("步骤 3：安装驱动", indicatorX + 35, stepY + 5);
        Controls.Add(_lblStep3);

        stepY += 45;
        _lblStep4 = new Label { Text = Theme.Icons.DotInactive, Location = new Point(indicatorX, stepY), Size = new Size(20, 20), ForeColor = Theme.Colors.TextMuted, Font = Theme.Fonts.Body(14f / 11f), BackColor = Theme.Colors.Surface };
        _lblStep4Text = CreateStepLabel("步骤 4：验证安装", indicatorX + 35, stepY + 5);
        Controls.Add(_lblStep4);

        _lblStatus = new Label
        {
            Text = "准备开始...",
            Location = new Point(30, 300),
            Size = new Size(320, 22),
            ForeColor = Theme.Colors.TextMuted,
            BackColor = Theme.Colors.Surface,
            Font = Theme.Fonts.Caption(1f),
        };
        Controls.Add(_lblStatus);

        _lblPercent = new Label
        {
            Text = "0%",
            Location = new Point(360, 300),
            Size = new Size(60, 22),
            ForeColor = Theme.Colors.Accent,
            BackColor = Theme.Colors.Surface,
            Font = Theme.Fonts.BodyBold(9.5f / 11f),
            TextAlign = ContentAlignment.TopRight,
        };
        Controls.Add(_lblPercent);

        _progressBar = new ProgressBar
        {
            Location = new Point(30, 265),
            Size = new Size(390, 22),
            Style = ProgressBarStyle.Continuous,
            BackColor = Theme.Colors.Elevated,
            ForeColor = Theme.Colors.Accent,
        };
        _progressBar.SetProgressBarColor(Theme.Colors.Accent);
        Controls.Add(_progressBar);

        _lblDetails = new Label
        {
            Text = "",
            Location = new Point(30, 225),
            Size = new Size(390, 30),
            ForeColor = Theme.Colors.TextMuted,
            BackColor = Theme.Colors.Surface,
            Font = Theme.Fonts.Small(1f),
        };
        Controls.Add(_lblDetails);

        _btnCancel = new Button
        {
            Text = "取消",
            Location = new Point(200, 330),
            Size = new Size(100, 30),
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.Colors.Elevated,
            ForeColor = Theme.Colors.TextPrimary,
            Cursor = Cursors.Hand,
        };
        _btnCancel.FlatAppearance.BorderColor = Theme.Colors.TextMuted;
        _btnCancel.FlatAppearance.MouseOverBackColor = Theme.Colors.ButtonHover;
        _btnCancel.Click += (_, _) => { _cancelled = true; _btnCancel.Enabled = false; _btnCancel.Text = "取消中..."; };
        Controls.Add(_btnCancel);

        Paint += (_, e) =>
        {
            var g = e.Graphics;
            using var bg = new LinearGradientBrush(ClientRectangle, Theme.Colors.Surface, Color.FromArgb(20, 20, 32), 90f);
            g.FillRectangle(bg, ClientRectangle);
        };
    }

    private Label CreateStepLabel(string text, int x, int y)
    {
        var lbl = new Label { Text = text, Location = new Point(x, y), Size = new Size(350, 20), ForeColor = Theme.Colors.TextMuted, BackColor = Theme.Colors.Surface, Font = Theme.Fonts.Body(10f / 11f) };
        Controls.Add(lbl);
        return lbl;
    }

    public void SetStep(int step, string status, int progress, string details = "")
    {
        if (IsDisposed) return;
        BeginInvoke(() =>
        {
            Label[] indicators = { _lblStep1, _lblStep2, _lblStep3, _lblStep4 };
            Label[] texts = { _lblStep1Text, _lblStep2Text, _lblStep3Text, _lblStep4Text };

            for (int i = 0; i < indicators.Length; i++)
            {
                if (i < step)
                {
                    indicators[i].Text = Theme.Icons.DotActive;
                    indicators[i].ForeColor = Theme.Colors.Success;
                    texts[i].ForeColor = Theme.Colors.Success;
                }
                else if (i == step)
                {
                    indicators[i].Text = Theme.Icons.DotProgress;
                    indicators[i].ForeColor = Theme.Colors.Accent;
                    texts[i].ForeColor = Theme.Colors.Accent;
                }
                else
                {
                    indicators[i].Text = Theme.Icons.DotInactive;
                    indicators[i].ForeColor = Theme.Colors.TextMuted;
                    texts[i].ForeColor = Theme.Colors.TextMuted;
                }
            }

            _lblStatus.Text = status;
            _lblDetails.Text = details;
            _progressBar.Value = Math.Min(100, Math.Max(0, progress));
            _lblPercent.Text = $"{progress}%";

            if (_cancelled)
            {
                _lblStatus.Text = "用户已取消安装";
                _lblStatus.ForeColor = Theme.Colors.Warning;
            }
        });
    }

    public void ShowError(string message)
    {
        if (IsDisposed) return;
        BeginInvoke(() =>
        {
            _lblStatus.Text = "安装失败";
            _lblStatus.ForeColor = Theme.Colors.Error;
            ResultMessage = message;
            _btnCancel.Text = "关闭";
            _btnCancel.Enabled = true;
        });
    }

    public void ShowSuccess()
    {
        if (IsDisposed) return;
        BeginInvoke(() =>
        {
            _lblStep4.Text = Theme.Icons.DotActive;
            _lblStep4.ForeColor = Theme.Colors.Success;
            _lblStep4Text.ForeColor = Theme.Colors.Success;
            _lblStatus.Text = "安装成功！";
            _lblStatus.ForeColor = Theme.Colors.Success;
            _progressBar.Value = 100;
            _lblPercent.Text = "100%";
            Success = true;
            ResultMessage = "VB-Audio Virtual Cable 安装成功！";
            DialogResult = DialogResult.OK;
            Close();
        });
    }

    public bool IsCancelled => _cancelled;
}

public static class ProgressBarExtensions
{
    public static void SetProgressBarColor(this ProgressBar pbar, Color color)
    {
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            SendMessage(pbar.Handle, 0x409, 0, (int)color.R | (color.G << 8) | (color.B << 16));
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
}
