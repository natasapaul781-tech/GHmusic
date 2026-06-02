using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using NAudio.Wave;

namespace Soundboard;

public record DeviceItem(int Number, string Display)
{
    public override string ToString() => Display;
}

public class MainForm : Form
{
    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);


    private const int VK_CONTROL = 0x11;
    private const int VK_SHIFT = 0x10;
    private const int VK_MENU = 0x12;
    private const int VK_LMENU = 0xA4;
    private const int VK_RMENU = 0xA5;

    private const int WM_MOUSEWHEEL = 0x020A;
    private const int WM_DEVICECHANGE = 0x0219;
    private const int WM_HOTKEY = 0x0312;
    private const int WM_INPUT = 0x00FF;
    private const int DBT_DEVICEARRIVAL = 0x8000;
    private const int DBT_DEVICEREMOVECOMPLETE = 0x8004;

    private const int HOTKEY_ID_PLAY = 1;
    private const int HOTKEY_ID_STOP = 2;
    private const int HOTKEY_ID_CARD_BASE = 100;

    private static int SideWidth => (int)(500 * _uiScale);

    private DoubleBufferedFlowPanel _cardFlow = null!;
    private Panel _topBar = null!;
    private Panel _footerBar = null!;
    private Panel _sidePanel = null!;
    private Panel _sideOverlay = null!;
    private AnimatedButton _btnSettings = null!;
    private AnimatedButton _btnStopAll = null!;
    private Label _lblFooterStatus = null!;
    private Label _lblFooterPtt = null!;
    private ComboBox _cmbGame = null!;
    private ComboBox _cmbLocal = null!;
    private ComboBox _cmbMic = null!;
    private ThemedTrackBar _trkGameVol = null!;
    private ThemedTrackBar _trkLocalVol = null!;
    private ThemedTrackBar _trkMicVol = null!;
    private Label _lblGameVol = null!;
    private Label _lblLocalVol = null!;
    private Label _lblMicVol = null!;
    private ThemedTrackBar _trkDuration = null!;
    private Button _lblPttKey = null!;
    private Button _lblPlayKey = null!;
    private Button _lblStopKey = null!;
    private CheckBox _chkOverlap = null!;
    private CheckBox _chkStartMin = null!;
    private CheckBox _chkMicPassthrough = null!;
    private CheckBox _chkPttEnable = null!;
    private CheckBox _chkPttMode = null!;
    private ContextMenuStrip _cardMenu = null!;
    private NotifyIcon _trayIcon = null!;
    private ContextMenuStrip _trayMenu = null!;
    private DoubleBufferedPanel _sideInner = null!;
    private Label _lblVbCableHint = null!;

    private readonly BindingList<HotkeyBinding> _bindings = new();
    private AudioEngine _audio = null!;
    private AppConfig _config = null!;
    private VBCableManager _vbCable = new();
    private KeyHookManager _keyHook = null!;
    private bool _sideOpen;
    private bool _reallyClose;
    private bool _startupComplete;
    private System.Windows.Forms.Timer? _sideAnimTimer;
    private int _sideAnimStartX;
    private int _sideAnimTargetX;
    private long _sideAnimStartTicks;
    private const int SideAnimDurationMs = 180;
    private SoundCard? _selectedCard;
    private System.Windows.Forms.Timer? _saveTimer;
    private System.Windows.Forms.Timer? _hookHealthTimer;
    private CancellationTokenSource? _pollingCts;
    private readonly HashSet<int> _prevPressedKeys = new();
    private Label _lblVbStatus = null!;
    private Button _btnVbCable = null!;
    private ToolTip _tooltip = null!;
    private Label _lblDeviceStatus = null!;
    private static float _uiScale = 1.25f;
    private List<string> _deviceNames = new();
    private AudioPttDetector? _pttDetector;
    private long _lastPlayTicks;
    private bool _shownTrayTip;

    private Keys PlayTriggerKey => (Keys)_config.PlayTriggerKey;
    private Keys PlayTriggerMods => (Keys)_config.PlayTriggerModifiers;
    private Keys StopKey => (Keys)_config.StopKey;
    private Keys StopMods => (Keys)_config.StopModifiers;

    public MainForm()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
        DoubleBuffered = true;
        BuildIcon();
        BuildUI();
        _keyHook = new KeyHookManager();
        _keyHook.KeyTriggered += (k, m) => { if (!IsDisposed && IsHandleCreated) BeginInvoke(() => OnKeyTriggered(k, m)); };
        _keyHook.ModeChanged += m => { if (!IsDisposed && IsHandleCreated) BeginInvoke(() => UpdateFooter()); };
        _keyHook.Initialize();
        LoadConfig();
        _hookHealthTimer = new System.Windows.Forms.Timer { Interval = 3000 };
        _hookHealthTimer.Tick += (_, _) =>
        {
            if (IsDisposed || !IsHandleCreated) return;
            if (!_keyHook.IsHookResponsive())
            {
                _keyHook.Reinstall();
                RegisterAllHotKeys();
            }
        };
        _hookHealthTimer.Start();
        _pollingCts = new CancellationTokenSource();
        var pollToken = _pollingCts.Token;
        var pollThread = new Thread(() =>
        {
            while (!pollToken.IsCancellationRequested)
            {
                try { pollToken.WaitHandle.WaitOne(30); } catch { break; }
                if (pollToken.IsCancellationRequested) break;
                if (IsDisposed || !IsHandleCreated) continue;
                try { BeginInvoke(() => { if (!IsDisposed && IsHandleCreated) PollKeyState(); }); } catch { }
            }
        });
        pollThread.IsBackground = true;
        pollThread.Start();
        Shown += MainForm_Shown;
    }

    private void MainForm_Shown(object? sender, EventArgs e)
    {
        _startupComplete = true;
        if (_config.Bindings.Count == 0)
        {
            var wizard = new SetupWizardForm();
            wizard.ShowDialog(this);
            if (wizard.Completed)
            {
                foreach (var f in wizard.ImportedFiles)
                    TryAddFile(f);
                if (wizard.WizardPlayKey != Keys.None)
                {
                    _config.PlayTriggerKey = (int)wizard.WizardPlayKey;
                    _config.PlayTriggerModifiers = (int)wizard.WizardPlayModifiers;
                    ApplyAudioKeys();
                    SaveConfig();
                }
                RefreshDeviceList();
                UpdateVBCableStatus();
                UpdateVbCableHint();
            }
        }
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(Theme.Colors.Bg);
    }

    private void BuildUI()
    {
        SuspendLayout();
        Text = "Soundboard - 音效播放面板";
        Size = new Size((int)(1150 * _uiScale), (int)(800 * _uiScale));
        MinimumSize = new Size((int)(850 * _uiScale), (int)(580 * _uiScale));
        BackColor = Theme.Colors.Bg;
        ForeColor = Theme.Colors.TextPrimary;
        Font = Theme.Fonts.Body(_uiScale);
        StartPosition = FormStartPosition.CenterScreen;
        AllowDrop = true;
        DragEnter += (_, e) => { if (e.Data!.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; };
        DragDrop += MainForm_DragDrop;
        FormClosing += MainForm_FormClosing;
        Resize += (_, _) =>
        {
            if (WindowState == FormWindowState.Minimized && _startupComplete) { HideToTray(); return; }
            if (_sideOpen && _sidePanel != null && _sideInner != null && _topBar != null)
            {
                _sideAnimTimer?.Stop();
                _sideOverlay.Bounds = new Rectangle(0, _topBar.Bottom, ClientSize.Width, _footerBar.Top - _topBar.Bottom);
                _sidePanel.Location = new Point(ClientSize.Width - SideWidth, _topBar.Bottom);
                _sidePanel.Height = _cardFlow.Height;
                var w = Math.Max(1, _sidePanel.ClientSize.Width);
                var h = Math.Max(1, _sidePanel.ClientSize.Height);
                _sideInner.Size = new Size(w, h);
            }
        };

        var cardPadding = (int)(42 * _uiScale);
        _cardFlow = new DoubleBufferedFlowPanel
        {
            Dock = DockStyle.Fill, AutoScroll = true,
            Padding = new Padding(cardPadding, (int)(16 * _uiScale), cardPadding, cardPadding),
            BackColor = Theme.Colors.Bg, WrapContents = true,
        };
        Controls.Add(_cardFlow);

        _topBar = new Panel { Dock = DockStyle.Top, Height = (int)(44 * _uiScale), BackColor = Theme.Colors.Surface };
        _topBar.Paint += (_, e) =>
        {
            var r = _topBar.ClientRectangle;
            using var p = new Pen(Theme.Colors.Border);
            e.Graphics.DrawLine(p, 0, r.Height - 1, r.Width, r.Height - 1);
        };
        Controls.Add(_topBar);

        _btnStopAll = new AnimatedButton
        {
            Text = $"{Theme.Icons.Stop} 停止",
            Anchor = AnchorStyles.Left,
            Size = new Size((int)(100 * _uiScale), (int)(34 * _uiScale)), Cursor = Cursors.Hand,
            Font = Theme.Fonts.Caption(_uiScale),
        };
        _btnStopAll.SetColors(Theme.Colors.Surface, Theme.Colors.ButtonHover);
        _btnStopAll.ForeColor = Theme.Colors.Error;
        _btnStopAll.Click += (_, _) => { _audio?.StopAll(); ClearAllPlaying(); UpdateFooter(); };
        _topBar.Controls.Add(_btnStopAll);

        _btnSettings = new AnimatedButton
        {
            Text = Theme.Icons.Settings,
            Anchor = AnchorStyles.Right,
            Size = new Size((int)(44 * _uiScale), (int)(34 * _uiScale)), Cursor = Cursors.Hand,
            BackColor = Theme.Colors.Surface,
            ForeColor = Theme.Colors.TextSecondary, Font = Theme.Fonts.Title(_uiScale),
        };
        _btnSettings.SetColors(Theme.Colors.Surface, Theme.Colors.ButtonHover);
        _btnSettings.FlatAppearance.BorderSize = 0;
        _btnSettings.FlatAppearance.MouseOverBackColor = Theme.Colors.ButtonHover;
        _btnSettings.Click += (_, _) => ToggleSidePanel();
        _topBar.Controls.Add(_btnSettings);

        _topBar.Layout += (_, _) => LayoutTopBar();

        _footerBar = new Panel { Dock = DockStyle.Bottom, Height = (int)(56 * _uiScale), BackColor = Theme.Colors.FooterBg };
        _footerBar.Paint += (_, e) => { using var p = new Pen(Theme.Colors.Border); e.Graphics.DrawLine(p, 0, 0, _footerBar.Width, 0); };
        Controls.Add(_footerBar);

        _lblFooterStatus = new Label
        {
            Location = new Point((int)(16 * _uiScale), (int)(6 * _uiScale)),
            Size = new Size((int)(200 * _uiScale), (int)(22 * _uiScale)),
            AutoSize = false, AutoEllipsis = true,
            ForeColor = Theme.Colors.TextSecondary, BackColor = Theme.Colors.FooterBg,
            Font = Theme.Fonts.Small(_uiScale),
        };
        _footerBar.Controls.Add(_lblFooterStatus);

        _lblFooterPtt = new Label
        {
            Location = new Point((int)(224 * _uiScale), (int)(6 * _uiScale)),
            Size = new Size((int)(120 * _uiScale), (int)(22 * _uiScale)),
            AutoSize = false, AutoEllipsis = true,
            ForeColor = Theme.Colors.TextSecondary, BackColor = Theme.Colors.FooterBg,
            Font = Theme.Fonts.Small(_uiScale),
        };
        _footerBar.Controls.Add(_lblFooterPtt);

        _lblDeviceStatus = new Label
        {
            Location = new Point((int)(16 * _uiScale), (int)(32 * _uiScale)),
            Size = new Size((int)(700 * _uiScale), (int)(22 * _uiScale)),
            AutoSize = false, AutoEllipsis = true,
            ForeColor = Theme.Colors.TextSecondary, BackColor = Theme.Colors.FooterBg,
            Font = Theme.Fonts.Small(_uiScale),
            TextAlign = ContentAlignment.MiddleLeft,
        };
        _footerBar.Controls.Add(_lblDeviceStatus);

        _footerBar.Layout += (_, _) => LayoutFooter();

        _cardMenu = new ContextMenuStrip
        {
            BackColor = Theme.Colors.Surface, ForeColor = Theme.Colors.TextPrimary,
            Renderer = new ToolStripProfessionalRenderer(new DarkColorTable()),
        };
        _cardMenu.Items.Add($"{Theme.Icons.Play} 播放").Click += (_, _) => { if (_selectedCard?.Binding != null) PlayFile(_selectedCard.Binding.FilePath); };
        _cardMenu.Items.Add($"{Theme.Icons.KeyBind} 设置快捷键").Click += (_, _) => SetKeyForSelectedCard();
        _cardMenu.Items.Add($"{Theme.Icons.Trim} 设置播放区间").Click += (_, _) => SetTrimForSelectedCard();
        _cardMenu.Items.Add(new ToolStripSeparator());
        _cardMenu.Items.Add($"{Theme.Icons.Delete} 删除").Click += (_, _) => DeleteSelectedCard();

        _tooltip = new ToolTip
        {
            AutoPopDelay = 8000,
            InitialDelay = 500,
            ReshowDelay = 250,
            ShowAlways = true,
            BackColor = Theme.Colors.Surface,
            ForeColor = Theme.Colors.TextPrimary,
        };

        BuildSidePanel();

        _trayMenu = new ContextMenuStrip
        {
            BackColor = Theme.Colors.Surface, ForeColor = Theme.Colors.TextPrimary,
            Renderer = new ToolStripProfessionalRenderer(new DarkColorTable()),
        };
        _trayMenu.Items.Add("显示窗口").Click += (_, _) => ShowFromTray();
        _trayMenu.Items.Add("停止所有播放").Click += (_, _) => _audio?.StopAll();
        _trayMenu.Items.Add(new ToolStripSeparator());
        _trayMenu.Items.Add("退出").Click += (_, _) => { _reallyClose = true; Application.Exit(); };
        _trayIcon = new NotifyIcon { Text = "Soundboard", Icon = this.Icon!, ContextMenuStrip = _trayMenu, Visible = false };
        _trayIcon.DoubleClick += (_, _) => ShowFromTray();

        ResumeLayout(false);
        PerformLayout();
    }

    private void BuildSidePanel()
    {
        _sideOverlay = new Panel { BackColor = Theme.Colors.Overlay, Visible = false };
        _sideOverlay.Click += (_, _) => ToggleSidePanel();
        _sideOverlay.MouseDown += (_, _) => ToggleSidePanel();
        Controls.Add(_sideOverlay);

        _sidePanel = new Panel { Size = new Size(SideWidth, 700), BackColor = Theme.Colors.Surface, Visible = false, Anchor = AnchorStyles.Top | AnchorStyles.Right };
        Controls.Add(_sidePanel);
        _sidePanel.Resize += (_, _) =>
        {
            if (_sideInner != null)
            {
                var w = Math.Max(1, _sidePanel.ClientSize.Width);
                var h = Math.Max(1, _sidePanel.ClientSize.Height);
                if (w != _sideInner.Width || h != _sideInner.Height)
                    _sideInner.Size = new Size(w, h);
            }
        };
        _sidePanel.Paint += (_, e) =>
        {
            var g = e.Graphics;
            var r = _sidePanel.ClientRectangle;
            using var bg = new LinearGradientBrush(r, Theme.Colors.Surface, Color.FromArgb(18, 18, 32), 90f);
            g.FillRectangle(bg, r);
            using var p = new Pen(Theme.Colors.Border);
            g.DrawLine(p, 0, 0, r.Width, 0);
        };

        _sideInner = new DoubleBufferedPanel
        {
            Location = new Point(0, 0),
            Size = new Size(SideWidth, SideWidth),
            AutoScroll = true,
            BackColor = Theme.Colors.Surface,
        };
        _sidePanel.Controls.Add(_sideInner);

        ThemedScrollBar.ApplyToControl(_sideInner);
        int sideW = _sideInner.ClientSize.Width - SystemInformation.VerticalScrollBarWidth;

        var y = (int)(40 * _uiScale);

        AddSideSection("热键设置", ref y);
        _lblPttKey = AddSideKeyRow("语音激活键 (PTT)", "V", ref y,
            () => OpenKeyDialog("语音激活键 (PTT)", "游戏中按住此键即可说话 — 播放时自动保持按下",
                (Keys)_config.PttKey, (Keys)_config.PttModifiers,
                (k, m) => { _config.PttKey = (int)k; _config.PttModifiers = (int)m; ApplyAudioKeys(); }));
        _lblPlayKey = AddSideKeyRow("播放触发键", "P", ref y,
            () => OpenKeyDialog("播放触发键", "按下此键开始播放选中的音效到队内语音",
                (Keys)_config.PlayTriggerKey, (Keys)_config.PlayTriggerModifiers,
                (k, m) => { _config.PlayTriggerKey = (int)k; _config.PlayTriggerModifiers = (int)m; ApplyAudioKeys(); }));
        _lblStopKey = AddSideKeyRow("播放终止键", "Ctrl+S", ref y,
            () => OpenKeyDialog("播放终止键", "按下此键立即停止所有音频播放",
                (Keys)_config.StopKey, (Keys)_config.StopModifiers,
                (k, m) => { _config.StopKey = (int)k; _config.StopModifiers = (int)m; ApplyAudioKeys(); }));

        y += (int)(36 * _uiScale);

        AddSideSection("播放设置", ref y);

        var lblDur = new Label { Text = "播放时长", Location = new Point((int)(24 * _uiScale), y), AutoSize = true, ForeColor = Theme.Colors.TextSecondary, BackColor = Theme.Colors.Surface, Font = Theme.Fonts.Body(_uiScale) };
        _sideInner.Controls.Add(lblDur);
        var lblDurVal = new Label { Text = "15 秒", Location = new Point(sideW - (int)(88 * _uiScale), y), AutoSize = true, ForeColor = Theme.Colors.Accent, BackColor = Theme.Colors.Surface, Font = Theme.Fonts.BodyBold(_uiScale) };
        _sideInner.Controls.Add(lblDurVal);
        y += (int)(48 * _uiScale);

        _trkDuration = new ThemedTrackBar
        {
            Location = new Point((int)(24 * _uiScale), y),
            Size = new Size(sideW - (int)(60 * _uiScale), (int)(34 * _uiScale)),
            Minimum = 1, Maximum = 60, Value = 15, TickStyle = TickStyle.None, BackColor = Theme.Colors.Surface,
        };
        _trkDuration.ValueChanged += (_, _) =>
        {
            lblDurVal.Text = $"{_trkDuration.Value} 秒";
            _config.PlayDurationSeconds = _trkDuration.Value;
            if (_audio != null) _audio.PlayDurationMs = _trkDuration.Value * 1000;
            SaveConfigDelayed();
        };
        _sideInner.Controls.Add(_trkDuration);
        SuppressMouseWheel(_trkDuration);
        y += (int)(64 * _uiScale);

        _chkOverlap = new CheckBox { Text = "允许重叠播放", Location = new Point((int)(24 * _uiScale), y), Size = new Size(sideW - (int)(48 * _uiScale), (int)(38 * _uiScale)), ForeColor = Theme.Colors.TextPrimary, BackColor = Theme.Colors.Surface, FlatStyle = FlatStyle.Flat, Font = Theme.Fonts.Body(_uiScale) };
        _chkOverlap.FlatAppearance.BorderColor = Theme.Colors.Border;
        _chkOverlap.FlatAppearance.CheckedBackColor = Theme.Colors.ButtonDefault;
        _chkOverlap.FlatAppearance.MouseOverBackColor = Theme.Colors.Elevated;
        _chkOverlap.CheckedChanged += (_, _) => SaveConfigDelayed();
        _sideInner.Controls.Add(_chkOverlap);
        y += (int)(60 * _uiScale);

        _chkPttEnable = new CheckBox { Text = "启用 PTT 自动按键\n（播放时自动模拟按住语音键）", Location = new Point((int)(24 * _uiScale), y), Size = new Size(sideW - (int)(48 * _uiScale), (int)(52 * _uiScale)), ForeColor = Theme.Colors.TextPrimary, BackColor = Theme.Colors.Surface, FlatStyle = FlatStyle.Flat, Font = Theme.Fonts.Body(_uiScale) };
        _chkPttEnable.FlatAppearance.BorderColor = Theme.Colors.Border;
        _chkPttEnable.FlatAppearance.CheckedBackColor = Theme.Colors.ButtonDefault;
        _chkPttEnable.FlatAppearance.MouseOverBackColor = Theme.Colors.Elevated;
        _chkPttEnable.CheckedChanged += (_, _) =>
        {
            if (_audio != null) _audio.PttEnabled = _chkPttEnable.Checked;
            SaveConfigDelayed(); UpdateFooter();
        };
        _sideInner.Controls.Add(_chkPttEnable);
        y += (int)(68 * _uiScale);

        _chkPttMode = new CheckBox { Text = "PTT 持续模式\n（手动开启后持续模拟按住语音键）", Location = new Point((int)(24 * _uiScale), y), Size = new Size(sideW - (int)(48 * _uiScale), (int)(52 * _uiScale)), ForeColor = Theme.Colors.TextPrimary, BackColor = Theme.Colors.Surface, FlatStyle = FlatStyle.Flat, Font = Theme.Fonts.Body(_uiScale) };
        _chkPttMode.FlatAppearance.BorderColor = Theme.Colors.Border;
        _chkPttMode.FlatAppearance.CheckedBackColor = Theme.Colors.ButtonDefault;
        _chkPttMode.FlatAppearance.MouseOverBackColor = Theme.Colors.Elevated;
        _chkPttMode.CheckedChanged += (_, _) =>
        {
            if (_chkPttMode.Checked)
            {
                _audio?.PressPttManual();
            }
            else
            {
                _audio?.ReleasePttManual();
            }
            SaveConfigDelayed();
            UpdateFooter();
        };
        _sideInner.Controls.Add(_chkPttMode);
        y += (int)(68 * _uiScale);

        _chkMicPassthrough = new CheckBox { Text = "麦克风混音\n（物理麦克风音频混入游戏输出）", Location = new Point((int)(24 * _uiScale), y), Size = new Size(sideW - (int)(48 * _uiScale), (int)(52 * _uiScale)), ForeColor = Theme.Colors.TextPrimary, BackColor = Theme.Colors.Surface, FlatStyle = FlatStyle.Flat, Font = Theme.Fonts.Body(_uiScale) };
        _chkMicPassthrough.FlatAppearance.BorderColor = Theme.Colors.Border;
        _chkMicPassthrough.FlatAppearance.CheckedBackColor = Theme.Colors.ButtonDefault;
        _chkMicPassthrough.FlatAppearance.MouseOverBackColor = Theme.Colors.Elevated;
        _chkMicPassthrough.CheckedChanged += (_, _) =>
        {
            if (_audio != null) _audio.MicPassthroughEnabled = _chkMicPassthrough.Checked;
            SaveConfigDelayed();
        };
        _sideInner.Controls.Add(_chkMicPassthrough);
        y += (int)(68 * _uiScale);

        _chkStartMin = new CheckBox { Text = "启动时自动最小化到托盘", Location = new Point((int)(24 * _uiScale), y), Size = new Size(sideW - (int)(48 * _uiScale), (int)(38 * _uiScale)), ForeColor = Theme.Colors.TextPrimary, BackColor = Theme.Colors.Surface, FlatStyle = FlatStyle.Flat, Font = Theme.Fonts.Body(_uiScale) };
        _chkStartMin.FlatAppearance.BorderColor = Theme.Colors.Border;
        _chkStartMin.FlatAppearance.CheckedBackColor = Theme.Colors.ButtonDefault;
        _chkStartMin.FlatAppearance.MouseOverBackColor = Theme.Colors.Elevated;
        _chkStartMin.CheckedChanged += (_, _) => SaveConfigDelayed();
        _sideInner.Controls.Add(_chkStartMin);
        y += (int)(42 * _uiScale);

        AddSideSection("音频输出设备", ref y);

        AddDeviceCombo("游戏输出", ref y, out _cmbGame, (di) =>
        {
            UpdateAudioEngine(); SaveConfigDelayed();
            UpdateVbCableHint();
        });
        AddDeviceCombo("本地试听", ref y, out _cmbLocal, (_) =>
        {
            UpdateAudioEngine(); SaveConfigDelayed();
        });
        AddDeviceCombo("麦克风输出", ref y, out _cmbMic, (di) =>
        {
            if (_audio != null && di != null)
                _audio.MicDeviceNumber = di.Number;
            SaveConfigDelayed();
            UpdateVbCableHint();
        });
        y += (int)(28 * _uiScale);

        _lblVbCableHint = new Label
        {
            Location = new Point((int)(24 * _uiScale), y),
            Size = new Size(sideW - (int)(48 * _uiScale), (int)(54 * _uiScale)),
            ForeColor = Theme.Colors.Warning, BackColor = Theme.Colors.Surface, Font = Theme.Fonts.Caption(_uiScale),
        };
        _sideInner.Controls.Add(_lblVbCableHint);
        y += (int)(72 * _uiScale);

        _lblVbStatus = new Label
        {
            Location = new Point((int)(24 * _uiScale), y), AutoSize = true,
            ForeColor = Theme.Colors.TextSecondary, BackColor = Theme.Colors.Surface, Font = Theme.Fonts.Caption(_uiScale),
        };
        _sideInner.Controls.Add(_lblVbStatus);
        y += (int)(44 * _uiScale);

        _btnVbCable = new Button
        {
            Text = $"{Theme.Icons.Info} 安装 VB-Cable", Location = new Point((int)(24 * _uiScale), y),
            Size = new Size(sideW - (int)(48 * _uiScale), (int)(44 * _uiScale)),
            FlatStyle = FlatStyle.Flat, BackColor = Theme.Colors.ButtonDefault,
            ForeColor = Theme.Colors.Accent, Cursor = Cursors.Hand, Font = Theme.Fonts.Body(_uiScale),
            Visible = !VBCableManager.IsVBCableInstalled(),
        };
        _btnVbCable.FlatAppearance.BorderSize = 0;
        _btnVbCable.FlatAppearance.MouseOverBackColor = Theme.Colors.ButtonHover;
        _btnVbCable.Click += (_, _) => InstallVBCableAsync();
        _sideInner.Controls.Add(_btnVbCable);
        y += (int)(76 * _uiScale);

        AddSideSection("音量控制", ref y);
        AddVolumeRow("游戏音量", ref y, out _trkGameVol, out _lblGameVol, v =>
        {
            if (_audio != null) _audio.GameVolume = v;
        });
        AddVolumeRow("本地音量", ref y, out _trkLocalVol, out _lblLocalVol, v =>
        {
            if (_audio != null) _audio.LocalVolume = v;
        });
        AddVolumeRow("麦克风音量", ref y, out _trkMicVol, out _lblMicVol, v =>
        {
            if (_audio != null)
            {
                _audio.MicVolume = v;
                _audio.UpdateMicPassthroughVolume();
            }
        });

        y += (int)(36 * _uiScale);

        AddSideSection("预设管理", ref y);

        var btnPreset = new Button
        {
            Text = $"{Theme.Icons.Gear} 管理预设 (" + AppConfig.CurrentPresetName + ")",
            Location = new Point((int)(24 * _uiScale), y),
            Size = new Size(sideW - (int)(48 * _uiScale), (int)(44 * _uiScale)),
            FlatStyle = FlatStyle.Flat, BackColor = Theme.Colors.ButtonDefault,
            ForeColor = Theme.Colors.TextSecondary, Cursor = Cursors.Hand, Font = Theme.Fonts.Body(_uiScale),
        };
        btnPreset.FlatAppearance.BorderSize = 0;
        btnPreset.FlatAppearance.MouseOverBackColor = Theme.Colors.ButtonHover;
        btnPreset.Click += (_, _) => OpenPresetManager(btnPreset);
        _sideInner.Controls.Add(btnPreset);
        y += (int)(72 * _uiScale);

        var saveBtn = new Button
        {
            Text = $"{Theme.Icons.Save} 保存配置", Location = new Point((int)(24 * _uiScale), y),
            Size = new Size(sideW - (int)(48 * _uiScale), (int)(46 * _uiScale)),
            FlatStyle = FlatStyle.Flat, BackColor = Theme.Colors.ButtonPrimary,
            ForeColor = Theme.Colors.TextPrimary, Cursor = Cursors.Hand, Font = Theme.Fonts.BodyBold(_uiScale),
        };
        saveBtn.FlatAppearance.BorderSize = 0;
        saveBtn.FlatAppearance.MouseOverBackColor = Theme.Colors.ButtonPrimaryHover;
        saveBtn.Click += (_, _) => SaveConfig();
        _sideInner.Controls.Add(saveBtn);

        BindSidePanelTooltips();
    }

    private void AddSideSection(string title, ref int y)
    {
        var lbl = new Label
        {
            Text = title, Location = new Point((int)(24 * _uiScale), y), AutoSize = true,
            ForeColor = Theme.Colors.Accent, BackColor = Theme.Colors.Surface,
            Font = Theme.Fonts.Subtitle(_uiScale),
        };
        _sideInner.Controls.Add(lbl);
        y += (int)(56 * _uiScale);
    }

    private Button AddSideKeyRow(string label, string keyText, ref int y, Action onClick)
    {
        var lbl = new Label
        {
            Text = label, Location = new Point((int)(24 * _uiScale), y), AutoSize = true,
            ForeColor = Theme.Colors.TextSecondary, BackColor = Theme.Colors.Surface, Font = Theme.Fonts.Body(_uiScale),
        };
        _sideInner.Controls.Add(lbl);

        var btn = new AnimatedButton
        {
            Text = keyText, Location = new Point(_sideInner.ClientSize.Width - (int)(190 * _uiScale), y),
            Size = new Size((int)(166 * _uiScale), (int)(42 * _uiScale)),
            ForeColor = Theme.Colors.TextPrimary, Cursor = Cursors.Hand, Font = Theme.Fonts.BodyBold(_uiScale),
        };
        btn.SetColors(Theme.Colors.ButtonDefault, Theme.Colors.ButtonHover);
        btn.Click += (_, _) => onClick();
        _sideInner.Controls.Add(btn);

        y += (int)(64 * _uiScale);
        return btn;
    }

    private void AddDeviceCombo(string label, ref int y, out ComboBox combo, Action<DeviceItem?> onChange)
    {
        var lbl = new Label
        {
            Text = label, Location = new Point((int)(24 * _uiScale), y), AutoSize = true,
            ForeColor = Theme.Colors.TextSecondary, BackColor = Theme.Colors.Surface, Font = Theme.Fonts.Body(_uiScale),
        };
        _sideInner.Controls.Add(lbl);
        y += (int)(38 * _uiScale);

        combo = new ComboBox
        {
            Location = new Point((int)(24 * _uiScale), y),
            Size = new Size(_sideInner.ClientSize.Width - (int)(48 * _uiScale), (int)(40 * _uiScale)),
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Theme.Colors.Surface, ForeColor = Theme.Colors.TextPrimary,
            Font = Theme.Fonts.Body(_uiScale),
        };
        combo.FlatStyle = FlatStyle.Flat;
        _sideInner.Controls.Add(combo);
        SuppressMouseWheel(combo);
        var comboRef = combo;
        comboRef.SelectedIndexChanged += (_, _) =>
        {
            if (comboRef.SelectedItem is DeviceItem di) onChange(di);
        };
        y += (int)(64 * _uiScale);
    }

    private void AddVolumeRow(string label, ref int y, out ThemedTrackBar trk, out Label lblVal, Action<float> onChange)
    {
        var lbl = new Label
        {
            Text = label, Location = new Point((int)(24 * _uiScale), y), AutoSize = true,
            ForeColor = Theme.Colors.TextSecondary, BackColor = Theme.Colors.Surface, Font = Theme.Fonts.Body(_uiScale),
        };
        _sideInner.Controls.Add(lbl);
        lblVal = new Label
        {
            Text = "100%", Location = new Point(_sideInner.ClientSize.Width - (int)(78 * _uiScale), y), AutoSize = true,
            ForeColor = Theme.Colors.Accent, BackColor = Theme.Colors.Surface, Font = Theme.Fonts.BodyBold(_uiScale),
        };
        _sideInner.Controls.Add(lblVal);
        y += (int)(38 * _uiScale);

        trk = new ThemedTrackBar
        {
            Location = new Point((int)(24 * _uiScale), y),
            Size = new Size(_sideInner.ClientSize.Width - (int)(60 * _uiScale), (int)(34 * _uiScale)),
            Minimum = 0, Maximum = 100, Value = 100, TickStyle = TickStyle.None, BackColor = Theme.Colors.Surface,
        };
        var lblRef = lblVal;
        var trkRef = trk;
        trk.ValueChanged += (_, _) =>
        {
            lblRef.Text = $"{trkRef.Value}%";
            onChange(trkRef.Value / 100f);
            SaveConfigDelayed();
        };
        _sideInner.Controls.Add(trk);
        SuppressMouseWheel(trk);
        y += (int)(64 * _uiScale);
    }

    private void SuppressMouseWheel(Control ctrl)
    {
        ctrl.MouseWheel += (_, e) =>
        {
            ((HandledMouseEventArgs)e).Handled = true;
            if (_sideInner.IsHandleCreated)
                SendMessage(_sideInner.Handle, WM_MOUSEWHEEL, (IntPtr)(e.Delta << 16), IntPtr.Zero);
        };
    }

    private Button MakeFooterButton(string text, int x, int y, int w, int h, Color accent)
    {
        var btn = new Button
        {
            Text = text, Location = new Point(x, y), Size = new Size(w, h),
            FlatStyle = FlatStyle.Flat, BackColor = Theme.Colors.Surface,
            ForeColor = accent, Cursor = Cursors.Hand,
            Font = Theme.Fonts.BodyBold(_uiScale),
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.FlatAppearance.MouseOverBackColor = Theme.Colors.ButtonHover;
        return btn;
    }

    private void BindSidePanelTooltips()
    {
        var defs = TooltipDefinitions.All;
        _tooltip.SetToolTip(_lblPttKey, defs["ptt_key"]);
        _tooltip.SetToolTip(_lblPlayKey, defs["play_key"]);
        _tooltip.SetToolTip(_lblStopKey, defs["stop_key"]);
        _tooltip.SetToolTip(_trkDuration, defs["play_duration"]);
        _tooltip.SetToolTip(_chkOverlap, defs["allow_overlap"]);
        _tooltip.SetToolTip(_chkPttEnable, defs["ptt_enable"]);
        _tooltip.SetToolTip(_chkPttMode, defs["ptt_mode"]);
        _tooltip.SetToolTip(_chkMicPassthrough, defs["mic_passthrough"]);
        _tooltip.SetToolTip(_chkStartMin, defs["start_minimized"]);
        _tooltip.SetToolTip(_cmbGame, defs["game_output"]);
        _tooltip.SetToolTip(_cmbLocal, defs["local_output"]);
        _tooltip.SetToolTip(_cmbMic, defs["mic_output"]);
        _tooltip.SetToolTip(_trkGameVol, defs["game_volume"]);
        _tooltip.SetToolTip(_trkLocalVol, defs["local_volume"]);
        _tooltip.SetToolTip(_trkMicVol, defs["mic_volume"]);
        _tooltip.SetToolTip(_btnVbCable, defs["vb_cable"]);
        _tooltip.SetToolTip(_btnStopAll, defs["btn_stop_all"] + "Ctrl+S");
    }

    private void UpdateDeviceStatusLabel()
    {
        var gameDevice = (_cmbGame.SelectedItem as DeviceItem)?.Number ?? -1;
        var localDevice = (_cmbLocal.SelectedItem as DeviceItem)?.Number ?? -1;
        var parts = new List<string>();
        if (gameDevice >= 0 && gameDevice < WaveOut.DeviceCount)
            parts.Add($"{Theme.Icons.Check} 游戏输出");
        else
            parts.Add($"{Theme.Icons.Cross} 游戏输出");
        if (localDevice >= 0 && localDevice < WaveOut.DeviceCount)
            parts.Add($"{Theme.Icons.Check} 本地监听");
        else
            parts.Add($"{Theme.Icons.Cross} 本地监听");
        parts.Add($"输入:{_keyHook?.CurrentMode.ToString() ?? "N/A"}");
        _lblDeviceStatus.Text = string.Join("  |  ", parts);
        _lblDeviceStatus.ForeColor = gameDevice >= 0 && localDevice >= 0 ? Theme.Colors.Success : Theme.Colors.Warning;
    }

    private void ToggleSidePanel()
    {
        _sideOpen = !_sideOpen;
        if (_sideOpen)
        {
            _sideOverlay.Bounds = new Rectangle(0, _topBar.Bottom, ClientSize.Width, _footerBar.Top - _topBar.Bottom);
            _sidePanel.Location = new Point(ClientSize.Width, _topBar.Bottom);
            _sidePanel.Height = _cardFlow.Height;
            _sidePanel.Visible = true;
            _sideOverlay.Visible = true;
            _sideOverlay.SendToBack();
            _sidePanel.BringToFront();
            RefreshDeviceList();
            UpdateVBCableStatus();
            UpdateVbCableHint();
            AnimateSidePanel(ClientSize.Width - SideWidth);
        }
        else
        {
            AnimateSidePanel(ClientSize.Width);
        }
    }

    private void AnimateSidePanel(int targetX)
    {
        _sideAnimTargetX = targetX;
        _sideAnimStartX = _sidePanel.Left;
        _sideAnimStartTicks = DateTime.Now.Ticks;
        if (_sideAnimTimer == null)
        {
            _sideAnimTimer = new System.Windows.Forms.Timer { Interval = 8 };
            _sideAnimTimer.Tick += SideAnimTick;
        }
        _sideAnimTimer.Start();
    }

    private void SideAnimTick(object? sender, EventArgs e)
    {
        var elapsed = (DateTime.Now.Ticks - _sideAnimStartTicks) / TimeSpan.TicksPerMillisecond;
        var progress = Math.Min(1f, (float)elapsed / SideAnimDurationMs);
        var eased = Theme.Easing.EaseOut(progress);
        var x = (int)(_sideAnimStartX + (_sideAnimTargetX - _sideAnimStartX) * eased);
        _sidePanel.Left = x;

        if (progress >= 1f)
        {
            _sideAnimTimer!.Stop();
            _sidePanel.Left = _sideAnimTargetX;
            if (!_sideOpen)
            {
                _sidePanel.Visible = false;
                _sideOverlay.Visible = false;
            }
        }
    }

    private void RefreshDeviceList()
    {
        var count = WaveOut.DeviceCount;
        if (_deviceNames.Count != count + 1)
        {
            _deviceNames.Clear();
            _deviceNames.Add("无");
            for (int i = 0; i < count; i++)
                _deviceNames.Add(WaveOut.GetCapabilities(i).ProductName);
        }

        void Refresh(ComboBox combo, int selected)
        {
            var currentSelection = combo.SelectedItem as DeviceItem;
            var currentNum = currentSelection?.Number ?? selected;
            combo.BeginUpdate();
            combo.Items.Clear();
            for (int i = -1; i < count; i++)
            {
                var name = i == -1 ? "无" : _deviceNames[i + 1];
                var item = new DeviceItem(i, name);
                combo.Items.Add(item);
                if (i == currentNum) combo.SelectedItem = item;
            }
            if (combo.SelectedItem == null && combo.Items.Count > 0) combo.SelectedIndex = 0;
            combo.EndUpdate();
        }
        Refresh(_cmbGame, _config.GameDeviceNumber);
        Refresh(_cmbLocal, _config.LocalDeviceNumber);
        Refresh(_cmbMic, _config.MicDeviceNumber);
    }

    private void UpdateVbCableHint()
    {
        var hasVb = VBCableManager.IsVBCableInstalled();
        var selected = (_cmbMic.SelectedItem as DeviceItem)?.Display ?? "";
        if (!hasVb)
        {
            _lblVbCableHint.Text = $"{Theme.Icons.Warning}  未检测到 VB-Cable 虚拟声卡！\n点击下方按钮安装，安装后在游戏中选择 \"CABLE Output\" 作为输入设备。";
            _lblVbStatus.Text = "状态: VB-Cable 未安装";
        }
        else if (!selected.Contains("CABLE"))
        {
            _lblVbCableHint.Text = $"{Theme.Icons.Warning}  请在上方「麦克风输出」中选择 \"CABLE Input\"，\n然后在游戏/语音软件中选择 \"CABLE Output\" 作为输入设备。";
            _lblVbStatus.Text = "状态: VB-Cable 已安装但未配置";
        }
        else
        {
            _lblVbCableHint.Text = $"{Theme.Icons.Check}  VB-Cable 已配置！\n在游戏/语音软件中选择 \"CABLE Output\" 作为输入设备即可。";
            _lblVbStatus.Text = "状态: VB-Cable 已安装并配置";
        }
    }

    private void UpdateVBCableStatus()
    {
        var hasVb = VBCableManager.IsVBCableInstalled();
        _lblVbStatus.ForeColor = hasVb ? Theme.Colors.Success : Theme.Colors.Warning;
        _lblVbStatus.Text = $"VB-Cable 状态: {(hasVb ? "已安装" : "未安装")}";
        if (_btnVbCable != null)
            _btnVbCable.Visible = !hasVb;
    }

    private void OpenPresetManager(Button btnPreset)
    {
        var form = new PresetManagerForm();
        form.PresetChanged += () =>
        {
            LoadConfig();
            btnPreset.Text = $"{Theme.Icons.Gear} 管理预设 (" + AppConfig.CurrentPresetName + ")";
        };
        form.ShowDialog(this);
        btnPreset.Text = $"{Theme.Icons.Gear} 管理预设 (" + AppConfig.CurrentPresetName + ")";
    }

    private void InstallVBCableAsync()
    {
        _btnVbCable.Enabled = false;
        _btnVbCable.Text = "安装中...";

        var installForm = new VBCableInstallForm();
        installForm.Shown += (_, _) =>
        {
            Task.Run(async () =>
            {
                _vbCable.ProgressChanged += progress =>
                {
                    if (!installForm.IsDisposed)
                        installForm.SetStep(progress.Step, progress.Status, progress.Progress, progress.Details);
                };

                var result = await _vbCable.EnsureInstalledAsync();

                if (!installForm.IsDisposed)
                {
                    if (result.Success)
                    {
                        installForm.ShowSuccess();
                        BeginInvoke(() =>
                        {
                            RefreshDeviceList();
                            UpdateVBCableStatus();
                            UpdateVbCableHint();
                            MessageBox.Show(
                                $"{Theme.Icons.Check} VB-Audio Virtual Cable 安装成功！\n\n请在游戏的语音设置中将麦克风设为 \"CABLE Output\"。",
                                "安装成功",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);
                        });
                    }
                    else
                    {
                        if (!installForm.IsDisposed)
                            installForm.ShowError(result.Message);

                        BeginInvoke(() =>
                        {
                            _btnVbCable.Enabled = true;
                            _btnVbCable.Text = $"{Theme.Icons.Info} 安装 VB-Cable";
                            if (MessageBox.Show(
                                result.Message + "\n\n是否打开调试日志查看详情？",
                                "安装未完成",
                                MessageBoxButtons.YesNo,
                                MessageBoxIcon.Warning) == DialogResult.Yes)
                            {
                                try { Process.Start(new ProcessStartInfo { FileName = VBCableManager.GetLogPath(), UseShellExecute = true }); } catch { }
                            }
                        });
                    }
                }
            });
        };

        installForm.ShowDialog(this);
    }

    private void LoadConfig()
    {
        _config = AppConfig.Load();
        _bindings.Clear();
        foreach (var b in _config.Bindings) _bindings.Add(b);
        RebindCards();

        _audio?.Dispose();
        _audio = new AudioEngine
        {
            GameDeviceNumber = _config.GameDeviceNumber,
            LocalDeviceNumber = _config.LocalDeviceNumber,
            MicDeviceNumber = _config.MicDeviceNumber,
            GameVolume = _config.GameVolume,
            LocalVolume = _config.LocalVolume,
            MicVolume = _config.MicVolume,
            PlayDurationMs = _config.PlayDurationSeconds * 1000,
            PttKey = (Keys)_config.PttKey,
            PttModifiers = (Keys)_config.PttModifiers,
            PttEnabled = _config.PttEnable,
        };
        _audio.PlaybackStopped += _ =>
        {
            if (IsDisposed || !IsHandleCreated) return;
            BeginInvoke(() =>
            {
                if (IsDisposed || !IsHandleCreated) return;
                ClearAllPlaying();
                UpdateFooter();
            });
        };

        _trkDuration.Value = _config.PlayDurationSeconds;
        _chkOverlap.Checked = _config.AllowOverlap;
        _chkStartMin.Checked = _config.StartMinimized;
        _chkPttEnable.Checked = _config.PttEnable;
        _chkMicPassthrough.Checked = _config.MicPassthrough;

        _trkGameVol.Value = (int)(_config.GameVolume * 100);
        _trkLocalVol.Value = (int)(_config.LocalVolume * 100);
        _trkMicVol.Value = (int)(_config.MicVolume * 100);
        _lblGameVol.Text = $"{_trkGameVol.Value}%";
        _lblLocalVol.Text = $"{_trkLocalVol.Value}%";
        _lblMicVol.Text = $"{_trkMicVol.Value}%";

        RefreshDeviceList();
        UpdateFooter();
        UpdateVBCableStatus();
        ApplyAudioKeys();
        UpdateDeviceStatusLabel();

        if (_config.MicPassthrough)
            _audio.MicPassthroughEnabled = true;

        if (_config.StartMinimized)
            HideToTray();

        RegisterAllHotKeys();
        _keyHook.RegisterRawInput(Handle);

        _pttDetector?.Dispose();
        _pttDetector = new AudioPttDetector();
        _pttDetector.PttActivated += () =>
        {
            if (_config.PttEnable && _audio is { IsPlayingAny: false })
            {
                var path = _selectedCard?.Binding?.FilePath;
                var trimStart = _selectedCard?.Binding?.TrimStartSeconds ?? 0;
                var trimEnd = _selectedCard?.Binding?.TrimEndSeconds ?? 0;
                if (path != null)
                {
                    var filePath = path;
                    BeginInvoke(() =>
                    {
                        if (IsDisposed || !IsHandleCreated) return;
                        var now = DateTime.UtcNow.Ticks;
                        if (now - _lastPlayTicks < TimeSpan.TicksPerSecond) return;
                        _lastPlayTicks = now;
                        _audio.StopAll();
                        ClearAllPlaying();
                        _audio.PlayWithoutPtt(filePath, trimStart, trimEnd);
                        var bind = _bindings.FirstOrDefault(b => b.FilePath == filePath);
                        if (bind != null) bind.IsPlaying = true;
                        UpdateFooter();
                        foreach (Control c in _cardFlow.Controls)
                            if (c is SoundCard sc) sc.Invalidate();
                    });
                }
            }
        };
    }

    private void RebindCards()
    {
        _cardFlow.SuspendLayout();
        _cardFlow.Controls.Clear();
        foreach (var b in _bindings)
            AddCard(b);
        var addCard = new SoundCard { IsAddCard = true };
        addCard.CardClicked += (_, _) =>
        {
            SelectCard(null);
            AddFileDialog();
        };
        _cardFlow.Controls.Add(addCard);
        if (_bindings.Count == 0)
        {
            var emptyHint = new Label
            {
                Text = "♫ 拖拽音频文件到此处，或点击 + 号按钮添加音效\n支持 MP3、WAV、AIFF、AAC、M4A 格式",
                AutoSize = false,
                Size = new Size((int)(400 * _uiScale), (int)(60 * _uiScale)),
                ForeColor = Theme.Colors.TextMuted,
                BackColor = Theme.Colors.Bg,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = Theme.Fonts.Body(_uiScale),
            };
            _cardFlow.Controls.Add(emptyHint);
        }
        _cardFlow.ResumeLayout(true);
        RegisterAllHotKeys();
    }

    private void SelectCard(SoundCard? card)
    {
        if (_selectedCard == card) return;
        if (_selectedCard != null) _selectedCard.IsSelected = false;
        _selectedCard = card;
        if (card != null) card.IsSelected = true;
    }

    private void AddCard(HotkeyBinding binding)
    {
        var card = new SoundCard { Binding = binding };
        card.CardClicked += (_, _) =>
        {
            SelectCard(card);
            PlayFile(binding.FilePath);
        };
        card.CardRightClicked += (_, _) =>
        {
            SelectCard(card);
            _cardMenu.Show(Cursor.Position);
        };
        _cardFlow.Controls.Add(card);
    }

    private void AddFileDialog()
    {
        var ofd = new OpenFileDialog
        {
            Filter = "音频文件|*.mp3;*.wav;*.aiff;*.aac;*.m4a|所有文件|*.*",
            Title = "选择音效文件",
            Multiselect = true,
        };
        if (ofd.ShowDialog() == DialogResult.OK)
        {
            foreach (var f in ofd.FileNames)
                TryAddFile(f);
        }
    }

    private bool TryAddFile(string path)
    {
        try
        {
            Cursor.Current = Cursors.WaitCursor;
            var dur = GetAudioDuration(path);
            var b = new HotkeyBinding { FilePath = path, DurationSeconds = dur };
            _bindings.Add(b);
            SaveConfigDelayed();
            RebindCards();
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"无法加载文件 \"{Path.GetFileName(path)}\":\n{ex.Message}", "加载失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
        finally
        {
            Cursor.Current = Cursors.Default;
        }
    }

    private void PlayFile(string path)
    {
        if (_audio == null) return;
        var now = DateTime.UtcNow.Ticks;
        if (now - _lastPlayTicks < TimeSpan.TicksPerSecond) return;
        _lastPlayTicks = now;
        if (!_config.AllowOverlap)
        {
            _audio.StopAll();
            ClearAllPlaying();
        }
        var binding = _bindings.FirstOrDefault(b => b.FilePath == path);
        if (binding != null && binding.HasTrim)
            _audio.PlayWithoutPtt(path, binding.TrimStartSeconds, binding.TrimEndSeconds);
        else
            _audio.PlayWithoutPtt(path);
        if (binding != null)
        {
            binding.IsPlaying = true;
            foreach (Control c in _cardFlow.Controls)
                if (c is SoundCard sc && sc.Binding == binding)
                    sc.Invalidate();
        }
        UpdateFooter();
    }

    private void ClearAllPlaying()
    {
        foreach (Control c in _cardFlow.Controls)
            if (c is SoundCard sc && sc.Binding != null)
            {
                sc.Binding.IsPlaying = false;
                sc.Invalidate();
            }
    }

    private void UpdateFooter()
    {
        var playing = _audio?.PlayingCount ?? 0;
        _lblFooterStatus.Text = playing > 0 ? $"正在播放:{playing}个" : "就绪";
        _lblFooterStatus.ForeColor = playing > 0 ? Theme.Colors.Success : Theme.Colors.TextSecondary;
        var pttActive = _pttDetector?.IsPttActive == true;
        var pttModeActive = _audio?.IsPttDown == true;
        if (pttModeActive)
        {
            _lblFooterPtt.Text = "PTT:持续开麦中";
            _lblFooterPtt.ForeColor = Theme.Colors.Success;
        }
        else if (pttActive)
        {
            _lblFooterPtt.Text = "PTT:语音检测中";
            _lblFooterPtt.ForeColor = Theme.Colors.Success;
        }
        else
        {
            _lblFooterPtt.Text = _audio?.PttEnabled == true ? "PTT:音频监听" : "PTT:关";
            _lblFooterPtt.ForeColor = Theme.Colors.TextSecondary;
        }
    }

    private void LayoutFooter()
    {
        if (_lblFooterStatus == null || _lblFooterPtt == null || _lblDeviceStatus == null)
            return;
        var w = _footerBar.ClientSize.Width;
        var h = _footerBar.ClientSize.Height;
        var rowH = (int)(22 * _uiScale);
        var pad = (int)(16 * _uiScale);
        _lblFooterStatus.SetBounds(pad, (int)(h * 0.12), (int)(w * 0.22), rowH);
        _lblFooterPtt.SetBounds(_lblFooterStatus.Right + (int)(12 * _uiScale), _lblFooterStatus.Top, (int)(w * 0.15), rowH);
        _lblDeviceStatus.SetBounds(pad, (int)(h * 0.55), w - pad * 2, rowH);
    }

    private void LayoutTopBar()
    {
        if (_btnStopAll == null || _btnSettings == null)
            return;
        var w = _topBar.ClientSize.Width;
        var cy = _topBar.ClientSize.Height / 2;
        var pad = (int)(8 * _uiScale);
        _btnStopAll.Location = new Point(pad, cy - _btnStopAll.Height / 2);
        _btnSettings.Location = new Point(w - _btnSettings.Width - pad, cy - _btnSettings.Height / 2);
    }

    private void SetKeyForSelectedCard()
    {
        if (_selectedCard?.Binding == null) return;
        var existingBindings = _bindings.Where(b => b != _selectedCard.Binding).ToList();
        OpenKeyDialog("设置快捷键", $"为 \"{Path.GetFileName(_selectedCard.Binding.FilePath)}\" 设置快捷键",
            (Keys)_selectedCard.Binding.Key, (Keys)_selectedCard.Binding.Modifiers,
            (k, m) =>
            {
                var conflict = existingBindings.FirstOrDefault(b => b.Key == (int)k && b.Modifiers == (int)m);
                if (conflict != null)
                {
                    var result = MessageBox.Show(
                        $"快捷键 {FormatKey(k, m)} 已被 \"{Path.GetFileName(conflict.FilePath)}\" 使用。\n\n是否覆盖？",
                        "热键冲突", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (result == DialogResult.No) return;
                    conflict.Key = (int)Keys.None;
                    conflict.Modifiers = 0;
                }
                string? globalConflict = null;
                if ((int)k == _config.PlayTriggerKey && (int)m == _config.PlayTriggerModifiers)
                    globalConflict = "播放触发键";
                else if ((int)k == _config.StopKey && (int)m == _config.StopModifiers)
                    globalConflict = "停止键";
                if (globalConflict != null)
                {
                    MessageBox.Show(
                        $"快捷键 {FormatKey(k, m)} 与全局{globalConflict}冲突，无法使用。",
                        "热键冲突", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                _selectedCard.Binding.Key = (int)k;
                _selectedCard.Binding.Modifiers = (int)m;
                SaveConfigDelayed();
                _selectedCard.Invalidate();
            });
    }

    private void SetTrimForSelectedCard()
    {
        if (_selectedCard?.Binding == null) return;
        var b = _selectedCard.Binding;
        var totalDur = b.DurationSeconds;
        var localDev = (_cmbLocal.SelectedItem as DeviceItem)?.Number ?? _config.LocalDeviceNumber;
        var form = new WaveformTrimForm(b.FilePath, b.TrimStartSeconds, b.TrimEndSeconds, totalDur, localDev,
            () => { _audio?.StopAll(); ClearAllPlaying(); });
        if (form.ShowDialog(this) == DialogResult.OK && form.Confirmed)
        {
            b.TrimStartSeconds = form.TrimStartSeconds;
            b.TrimEndSeconds = form.TrimEndSeconds;
            SaveConfigDelayed();
            _selectedCard.Invalidate();
        }
    }

    private void DeleteSelectedCard()
    {
        if (_selectedCard?.Binding == null) return;
        var res = MessageBox.Show($"确定删除 \"{Path.GetFileName(_selectedCard.Binding.FilePath)}\"?", "确认删除", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (res == DialogResult.Yes)
        {
            _bindings.Remove(_selectedCard.Binding);
            _selectedCard = null;
            SaveConfigDelayed();
            RebindCards();
        }
    }

    private void OpenKeyDialog(string title, string desc, Keys curKey, Keys curMods, Action<Keys, Keys> onSet)
    {
        var form = new Form
        {
            Text = title,
            Size = new Size((int)(420 * _uiScale), (int)(240 * _uiScale)),
            BackColor = Theme.Colors.Bg, ForeColor = Theme.Colors.TextPrimary, Font = Theme.Fonts.Body(_uiScale),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
        };
        var y = (int)(20 * _uiScale);
        var descLbl = new Label
        {
            Text = desc, Location = new Point((int)(24 * _uiScale), y),
            Size = new Size((int)(372 * _uiScale), (int)(48 * _uiScale)),
            ForeColor = Theme.Colors.TextSecondary, BackColor = Theme.Colors.Bg,
        };
        form.Controls.Add(descLbl);
        y += (int)(56 * _uiScale);

        var keyLbl = new Label
        {
            Text = curKey != Keys.None ? FormatKey(curKey, curMods) : "按下想要设置的按键...",
            Location = new Point((int)(24 * _uiScale), y),
            Size = new Size((int)(372 * _uiScale), (int)(50 * _uiScale)),
            ForeColor = curKey != Keys.None ? Theme.Colors.Success : Theme.Colors.TextPrimary,
            BackColor = Color.FromArgb(20, 20, 35),
            TextAlign = ContentAlignment.MiddleCenter, Font = Theme.Fonts.Subtitle(_uiScale),
            BorderStyle = BorderStyle.FixedSingle,
        };
        form.Controls.Add(keyLbl);

        Keys? selectedKey = curKey != Keys.None ? curKey : null;
        Keys selectedMods = curMods;

        form.KeyPreview = true;
        form.KeyDown += (_, e) =>
        {
            var modKeys = e.Modifiers;
            var key = e.KeyCode;
            if (key == Keys.Menu || key == Keys.ControlKey || key == Keys.ShiftKey || key == Keys.LWin || key == Keys.RWin)
                return;
            selectedKey = key;
            selectedMods = modKeys;
            e.Handled = true;
            keyLbl.Text = FormatKey(key, modKeys);
            keyLbl.ForeColor = Theme.Colors.Success;
        };
        y += (int)(64 * _uiScale);

        var btnOk = new Button
        {
            Text = "确定", Location = new Point((int)(24 * _uiScale), y),
            Size = new Size((int)(160 * _uiScale), (int)(40 * _uiScale)),
            FlatStyle = FlatStyle.Flat, BackColor = Theme.Colors.ButtonPrimary,
            ForeColor = Theme.Colors.TextPrimary, Cursor = Cursors.Hand, DialogResult = DialogResult.OK,
        };
        btnOk.FlatAppearance.BorderSize = 0;
        btnOk.Click += (_, _) =>
        {
            if (selectedKey.HasValue) onSet(selectedKey.Value, selectedMods);
            form.Close();
        };
        form.Controls.Add(btnOk);

        var btnCancel = new Button
        {
            Text = "取消", Location = new Point((int)(224 * _uiScale), y),
            Size = new Size((int)(160 * _uiScale), (int)(40 * _uiScale)),
            FlatStyle = FlatStyle.Flat, BackColor = Theme.Colors.Surface,
            ForeColor = Theme.Colors.TextPrimary, Cursor = Cursors.Hand, DialogResult = DialogResult.Cancel,
        };
        btnCancel.FlatAppearance.BorderSize = 0;
        form.Controls.Add(btnCancel);

        keyLbl.Text = FormatKey(curKey, curMods);
        form.ShowDialog(this);
    }

    private string FormatKey(Keys key, Keys mods)
    {
        var parts = new List<string>();
        if ((mods & Keys.Control) != 0) parts.Add("Ctrl");
        if ((mods & Keys.Shift) != 0) parts.Add("Shift");
        if ((mods & Keys.Alt) != 0) parts.Add("Alt");
        if (key != Keys.None) parts.Add(key.ToString());
        if (parts.Count == 0) return "未设置";
        return string.Join(" + ", parts);
    }

    private double GetAudioDuration(string path)
    {
        try
        {
            using var reader = new NAudio.Wave.AudioFileReader(path);
            var dur = reader.TotalTime.TotalSeconds;
            if (dur > 0) return dur;
        }
        catch { }

        try
        {
            using var reader = new NAudio.Wave.MediaFoundationReader(path);
            var dur = reader.TotalTime.TotalSeconds;
            if (dur > 0) return dur;
        }
        catch { }

        try
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".mp3")
            {
                using var reader = new NAudio.Wave.Mp3FileReader(path);
                return reader.TotalTime.TotalSeconds;
            }
            else
            {
                using var reader = new NAudio.Wave.WaveFileReader(path);
                return reader.TotalTime.TotalSeconds;
            }
        }
        catch { return 0; }
    }

    private void SaveConfigDelayed()
    {
        _saveTimer ??= new System.Windows.Forms.Timer { Interval = 500 };
        _saveTimer.Stop();
        _saveTimer.Tick -= OnSaveTimerTick;
        _saveTimer.Tick += OnSaveTimerTick;
        _saveTimer.Start();
    }

    private void OnSaveTimerTick(object? sender, EventArgs e)
    {
        _saveTimer?.Stop();
        SaveConfig();
    }

    private void SaveConfig()
    {
        _config.GameDeviceNumber = (_cmbGame.SelectedItem as DeviceItem)?.Number ?? -1;
        _config.LocalDeviceNumber = (_cmbLocal.SelectedItem as DeviceItem)?.Number ?? -1;
        _config.MicDeviceNumber = (_cmbMic.SelectedItem as DeviceItem)?.Number ?? -1;
        _config.GameVolume = _trkGameVol.Value / 100f;
        _config.LocalVolume = _trkLocalVol.Value / 100f;
        _config.MicVolume = _trkMicVol.Value / 100f;
        _config.PlayDurationSeconds = _trkDuration.Value;
        _config.AllowOverlap = _chkOverlap.Checked;
        _config.StartMinimized = _chkStartMin.Checked;
        _config.PttEnable = _chkPttEnable.Checked;
        _config.MicPassthrough = _chkMicPassthrough.Checked;
        _config.Bindings = new List<HotkeyBinding>(_bindings);
        _config.Save();
        UpdateFooter();
    }

    private void HideToTray()
    {
        ShowInTaskbar = false;
        _trayIcon.Visible = true;
        Hide();
        if (!_shownTrayTip)
        {
            _shownTrayTip = true;
            _trayIcon.ShowBalloonTip(3000, "Soundboard", "程序已最小化到系统托盘，仍在后台运行。右键托盘图标可退出。", ToolTipIcon.Info);
        }
    }

    private void ShowFromTray()
    {
        ShowInTaskbar = true;
        _trayIcon.Visible = false;
        Show();
        Activate();
    }

    private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!_reallyClose && e.CloseReason == CloseReason.UserClosing)
        {
            HideToTray();
            e.Cancel = true;
        }
        else
        {
            _pttDetector?.Dispose();
            try { _pollingCts?.Cancel(); _pollingCts?.Dispose(); } catch { }
            _hookHealthTimer?.Stop();
            _hookHealthTimer?.Dispose();
            _keyHook?.Dispose();
            _sideAnimTimer?.Stop();
            _sideAnimTimer?.Dispose();
            _saveTimer?.Stop();
            _saveTimer?.Dispose();
            _audio?.Dispose();
        }
    }

    private void PollKeyState()
    {
        if (_config == null) return;
        var candidates = new List<(int vk, Keys mods, Action action)>();
        var pk = (Keys)_config.PlayTriggerKey;
        if (pk != Keys.None)
            candidates.Add(((int)pk, (Keys)_config.PlayTriggerModifiers, () =>
            {
                var selected = _cardFlow.Controls.OfType<SoundCard>().FirstOrDefault(sc => sc == _selectedCard);
                if (selected?.Binding != null) PlayFile(selected.Binding.FilePath);
            }));
        var sk = (Keys)_config.StopKey;
        if (sk != Keys.None)
            candidates.Add(((int)sk, (Keys)_config.StopModifiers, () =>
            {
                _audio?.StopAll(); ClearAllPlaying(); UpdateFooter();
            }));
        var cardKeys = new HashSet<int>();
        for (int i = 0; i < _bindings.Count; i++)
        {
            var k = (Keys)_bindings[i].Key;
            if (k != Keys.None)
            {
                var idx = i;
                var vk = (int)k;
                cardKeys.Add(vk);
                candidates.Add((vk, (Keys)_bindings[i].Modifiers, () => PlayFile(_bindings[idx].FilePath)));
            }
        }
        var pttKey = (Keys)_config.PttKey;
        if (pttKey != Keys.None)
        {
            var pttVk = (int)pttKey;
            candidates.Add((pttVk, Keys.None, () =>
            {
                if (_cardFlow.Controls.OfType<SoundCard>().Any(sc => 
                    sc.Binding != null && (Keys)sc.Binding.Key != Keys.None && (GetAsyncKeyState(sc.Binding.Key) & 0x8000) != 0))
                    return;
                var selected = _cardFlow.Controls.OfType<SoundCard>().FirstOrDefault(sc => sc == _selectedCard);
                if (selected?.Binding != null) PlayFile(selected.Binding.FilePath);
            }));
        }
        foreach (var (vk, requiredMods, action) in candidates)
        {
            var isDown = (GetAsyncKeyState(vk) & 0x8000) != 0;
            var wasDown = _prevPressedKeys.Contains(vk);
            if (isDown && !wasDown)
            {
                _prevPressedKeys.Add(vk);
                var mods = Keys.None;
                if ((GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0) mods |= Keys.Control;
                if ((GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0) mods |= Keys.Shift;
                if ((GetAsyncKeyState(VK_MENU) & 0x8000) != 0
                    || (GetAsyncKeyState(VK_LMENU) & 0x8000) != 0
                    || (GetAsyncKeyState(VK_RMENU) & 0x8000) != 0) mods |= Keys.Alt;
                var req = requiredMods & (Keys.Control | Keys.Shift | Keys.Alt);
                if ((req & mods) == req)
                {
                    action();
                    break;
                }
            }
            else if (!isDown)
            {
                _prevPressedKeys.Remove(vk);
            }
        }
    }

    private void MainForm_DragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is not string[] files) return;
        foreach (var f in files) TryAddFile(f);
    }

    private void OnKeyTriggered(Keys key, Keys mods)
    {
        if (CheckMatch(key, mods, (Keys)_config.PlayTriggerKey, (Keys)_config.PlayTriggerModifiers))
        {
            var selected = _cardFlow.Controls.OfType<SoundCard>().FirstOrDefault(sc => sc == _selectedCard);
            if (selected?.Binding != null) PlayFile(selected.Binding.FilePath);
            return;
        }
        if (CheckMatch(key, mods, (Keys)_config.StopKey, (Keys)_config.StopModifiers))
        {
            _audio?.StopAll(); ClearAllPlaying(); UpdateFooter();
            return;
        }
        foreach (var b in _bindings)
        {
            if (CheckMatch(key, mods, (Keys)b.Key, (Keys)b.Modifiers))
            {
                PlayFile(b.FilePath);
                return;
            }
        }
    }

    private static uint ModsToWin32(Keys mods)
    {
        uint f = 0;
        if ((mods & Keys.Alt) != 0) f |= 0x0001;
        if ((mods & Keys.Control) != 0) f |= 0x0002;
        if ((mods & Keys.Shift) != 0) f |= 0x0004;
        return f;
    }

    private void HandleHotKey(int id)
    {
        // 不再使用 RegisterHotKey，改用 KeyHookManager 钩子处理
    }

    private void RegisterAllHotKeys()
    {
        // 不再使用 RegisterHotKey，改用 KeyHookManager 钩子处理
        // 这样可以避免按键被拦截，影响其他应用程序使用
    }

    private void UnregisterAllHotKeys()
    {
        // 不再使用 RegisterHotKey，改用 KeyHookManager 钩子处理
    }

    private bool CheckMatch(Keys pressedKey, Keys pressedMods, Keys targetKey, Keys targetMods)
    {
        if (targetKey == Keys.None) return false;
        if (pressedKey != targetKey) return false;
        var required = targetMods & (Keys.Control | Keys.Shift | Keys.Alt);
        if ((required & pressedMods) != required) return false;
        return true;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_DEVICECHANGE)
        {
            var wParam = m.WParam.ToInt32();
            if (wParam == DBT_DEVICEARRIVAL || wParam == DBT_DEVICEREMOVECOMPLETE)
            {
                BeginInvoke(() =>
                {
                    RefreshDeviceList();
                    UpdateDeviceStatusLabel();
                });
            }
        }
        if (m.Msg == WM_INPUT)
        {
            var result = KeyHookManager.ProcessRawInput(m.LParam);
            if (result.IsKeyDown && result.VirtualKey != Keys.None)
            {
                var mods = Keys.None;
                if ((ModifierKeys & Keys.Control) != 0) mods |= Keys.Control;
                if ((ModifierKeys & Keys.Shift) != 0) mods |= Keys.Shift;
                if ((ModifierKeys & Keys.Alt) != 0) mods |= Keys.Alt;
                OnKeyTriggered(result.VirtualKey, mods);
            }
        }
        base.WndProc(ref m);
    }

    private void BuildIcon()
    {
        var bmp = new Bitmap(64, 64);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Theme.Colors.Bg);
        using var brush = new SolidBrush(Theme.Colors.Accent);
        g.FillEllipse(brush, 8, 8, 48, 48);
        using var pen = new Pen(Theme.Colors.Success, 4);
        g.DrawArc(pen, 16, 20, 32, 28, -30, 120);
        g.DrawLine(pen, 32, 28, 32, 48);
        var hIcon = bmp.GetHicon();
        Icon = Icon.FromHandle(hIcon);
    }

    private void UpdateAudioEngine()
    {
        if (_audio == null) return;
        bool wasPassthrough = _audio.IsMicPassthroughActive;
        if (wasPassthrough) _audio.MicPassthroughEnabled = false;

        if (_cmbGame.SelectedItem is DeviceItem dGame) _audio.GameDeviceNumber = dGame.Number;
        if (_cmbLocal.SelectedItem is DeviceItem dLocal) _audio.LocalDeviceNumber = dLocal.Number;
        if (_cmbMic.SelectedItem is DeviceItem dMic) _audio.MicDeviceNumber = dMic.Number;
        _audio.GameVolume = _trkGameVol.Value / 100f;
        _audio.LocalVolume = _trkLocalVol.Value / 100f;
        _audio.MicVolume = _trkMicVol.Value / 100f;

        if (wasPassthrough) _audio.MicPassthroughEnabled = true;
    }

    private void ApplyAudioKeys()
    {
        if (_audio != null)
        {
            _audio.PttKey = (Keys)_config.PttKey;
            _audio.PttModifiers = (Keys)_config.PttModifiers;
        }
        _lblPttKey.Text = FormatKey((Keys)_config.PttKey, (Keys)_config.PttModifiers);
        _lblPlayKey.Text = FormatKey((Keys)_config.PlayTriggerKey, (Keys)_config.PlayTriggerModifiers);
        _lblStopKey.Text = FormatKey((Keys)_config.StopKey, (Keys)_config.StopModifiers);
        UpdateFooter();
    }

    private class DarkColorTable : ProfessionalColorTable
    {
        public override Color MenuItemSelected => Theme.Colors.ButtonHover;
        public override Color MenuBorder => Theme.Colors.Border;
        public override Color MenuItemBorder => Theme.Colors.Border;
        public override Color MenuStripGradientBegin => Theme.Colors.Surface;
        public override Color MenuStripGradientEnd => Theme.Colors.Surface;
        public override Color ToolStripDropDownBackground => Theme.Colors.Surface;
        public override Color ImageMarginGradientBegin => Theme.Colors.Surface;
        public override Color ImageMarginGradientMiddle => Theme.Colors.Surface;
        public override Color ImageMarginGradientEnd => Theme.Colors.Surface;
    }
}

public class DoubleBufferedFlowPanel : FlowLayoutPanel
{
    public DoubleBufferedFlowPanel()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
    }
}

public class DoubleBufferedPanel : Panel
{
    public DoubleBufferedPanel()
    {
        DoubleBuffered = true;
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor,
            true);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        if (BackColor.A == 0) return;
        e.Graphics.Clear(BackColor);
    }
}
