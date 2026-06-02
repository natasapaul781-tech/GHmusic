using System.Drawing.Drawing2D;

namespace HGmusic;

public class SetupWizardForm : Form
{
    private int _currentStep;
    private readonly Panel[] _stepPanels = new Panel[4];
    private readonly Label[] _stepDots = new Label[4];
    private readonly Button _btnPrev;
    private readonly Button _btnNext;
    private readonly Button _btnSkip;
    private readonly ListBox _fileListBox;
    private readonly Label _keyLabel;

    private Keys _wizardPlayKey;
    private Keys _wizardPlayModifiers;

    public List<string> ImportedFiles { get; } = new();
    public Keys WizardPlayKey => _wizardPlayKey;
    public Keys WizardPlayModifiers => _wizardPlayModifiers;
    public bool Completed { get; private set; }

    private static class C
    {
        // 主色调
        public static readonly Color Primary = Color.FromArgb(99, 102, 241);
        public static readonly Color PrimaryHover = Color.FromArgb(79, 82, 221);
        public static readonly Color PrimaryLight = Color.FromArgb(238, 240, 255);

        // 成功/警告/错误
        public static readonly Color Success = Color.FromArgb(16, 185, 129);
        public static readonly Color SuccessHover = Color.FromArgb(4, 150, 109);
        public static readonly Color Warning = Color.FromArgb(245, 158, 11);
        public static readonly Color Error = Color.FromArgb(239, 68, 68);

        // 文字
        public static readonly Color TextPrimary = Color.FromArgb(17, 24, 39);
        public static readonly Color TextSecondary = Color.FromArgb(107, 114, 128);
        public static readonly Color TextMuted = Color.FromArgb(156, 163, 175);
        public static readonly Color TextWhite = Color.White;

        // 背景
        public static readonly Color Bg = Color.FromArgb(249, 250, 251);
        public static readonly Color Surface = Color.White;
        public static readonly Color HeaderBg = Color.FromArgb(243, 244, 246);

        // 边框/阴影
        public static readonly Color Border = Color.FromArgb(229, 231, 235);
        public static readonly Color BorderLight = Color.FromArgb(243, 244, 246);

        // 活动指示
        public static readonly Color DotActive = Primary;
        public static readonly Color DotInactive = Color.FromArgb(209, 213, 219);
        public static readonly Color DotCompleted = Success;
    }

    public SetupWizardForm()
    {
        Text = "HGmusic 设置向导";
        Size = new Size(720, 560);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = C.Bg;
        ForeColor = C.TextPrimary;
        Font = new Font("Microsoft YaHei UI", 10f);
        KeyPreview = true;
        DoubleBuffered = true;

        // 主内容区域
        var contentArea = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = C.Bg,
            Padding = new Padding(0, 0, 0, 80),
        };
        Controls.Add(contentArea);

        // 步骤面板
        for (int i = 0; i < 4; i++)
        {
            _stepPanels[i] = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = C.Bg,
                Visible = i == 0,
                Padding = new Padding(40, 20, 40, 20),
            };
            contentArea.Controls.Add(_stepPanels[i]);
        }

        // 顶部步骤指示器
        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 100,
            BackColor = C.Surface,
            Padding = new Padding(40, 0, 40, 0),
        };
        headerPanel.Paint += (_, e) =>
        {
            using var pen = new Pen(C.Border);
            e.Graphics.DrawLine(pen, 0, headerPanel.Height - 1, headerPanel.Width, headerPanel.Height - 1);
        };
        Controls.Add(headerPanel);

        // 步骤指示器
        var stepLabels = new[] { "欢迎", "导入音频", "设置热键", "语音配置" };
        var dotStartX = 40;
        var dotSpacing = (720 - 80) / 4;

        for (int i = 0; i < 4; i++)
        {
            var x = dotStartX + i * dotSpacing + dotSpacing / 2;

            // 步骤圆点
            _stepDots[i] = new Label
            {
                Text = (i + 1).ToString(),
                Location = new Point(x - 16, 20),
                Size = new Size(32, 32),
                ForeColor = i == 0 ? C.TextWhite : C.TextMuted,
                BackColor = i == 0 ? C.Primary : C.DotInactive,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Microsoft YaHei UI", 12f, FontStyle.Bold),
            };
            _stepDots[i].Paint += (s, e) =>
            {
                var lbl = (Label)s!;
                using var path = new GraphicsPath();
                path.AddEllipse(2, 2, lbl.Width - 4, lbl.Height - 4);
                lbl.Region = new Region(path);
            };
            headerPanel.Controls.Add(_stepDots[i]);

            // 步骤标签
            var stepLabel = new Label
            {
                Text = stepLabels[i],
                Location = new Point(x - 40, 58),
                Size = new Size(80, 24),
                ForeColor = i == 0 ? C.Primary : C.TextMuted,
                BackColor = C.Surface,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Microsoft YaHei UI", 9f),
            };
            headerPanel.Controls.Add(stepLabel);
        }

        // 底部导航栏
        var navBar = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 80,
            BackColor = C.Surface,
            Padding = new Padding(40, 0, 40, 0),
        };
        navBar.Paint += (_, e) =>
        {
            using var pen = new Pen(C.Border);
            e.Graphics.DrawLine(pen, 0, 0, navBar.Width, 0);
        };
        Controls.Add(navBar);

        // 按钮样式
        var btnStyle = new Action<Button, bool>((btn, isPrimary) =>
        {
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.Cursor = Cursors.Hand;
            btn.Font = new Font("Microsoft YaHei UI", 10f, isPrimary ? FontStyle.Bold : FontStyle.Regular);
            btn.Size = new Size(120, 40);

            if (isPrimary)
            {
                btn.BackColor = C.Primary;
                btn.ForeColor = C.TextWhite;
                btn.FlatAppearance.MouseOverBackColor = C.PrimaryHover;
            }
            else
            {
                btn.BackColor = C.Bg;
                btn.ForeColor = C.TextSecondary;
                btn.FlatAppearance.MouseOverBackColor = C.HeaderBg;
            }
        });

        // 上一步按钮
        _btnPrev = new Button
        {
            Text = "← 上一步",
            Location = new Point(40, 20),
            Anchor = AnchorStyles.Left | AnchorStyles.Top,
        };
        btnStyle(_btnPrev, false);
        _btnPrev.Click += (_, _) => GoToStep(_currentStep - 1);
        navBar.Controls.Add(_btnPrev);

        // 跳过按钮
        _btnSkip = new Button
        {
            Text = "跳过向导",
            Location = new Point(720 - 40 - 120, 20),
            Anchor = AnchorStyles.Right | AnchorStyles.Top,
        };
        btnStyle(_btnSkip, false);
        _btnSkip.ForeColor = C.TextMuted;
        _btnSkip.Click += (_, _) => Close();
        navBar.Controls.Add(_btnSkip);

        // 下一步按钮
        _btnNext = new Button
        {
            Text = "下一步 →",
            Location = new Point(720 - 40 - 120 - 130, 20),
            Anchor = AnchorStyles.Right | AnchorStyles.Top,
        };
        btnStyle(_btnNext, true);
        _btnNext.Click += OnNextClick;
        navBar.Controls.Add(_btnNext);

        // 构建各步骤内容
        BuildStep0();
        _fileListBox = new ListBox();
        BuildStep1();
        _keyLabel = new Label();
        BuildStep2();
        BuildStep3();

        KeyDown += OnFormKeyDown;
        UpdateNavButtons();

        // 淡入动画
        Opacity = 0;
        Shown += (_, _) => AnimateFadeIn();
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

    private void BuildStep0()
    {
        var p = _stepPanels[0];

        // 欢迎图标
        var iconLabel = new Label
        {
            Text = "🎵",
            Location = new Point(0, 30),
            Size = new Size(720, 60),
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI Emoji", 32f),
            BackColor = C.Bg,
        };
        p.Controls.Add(iconLabel);

        // 欢迎标题
        var welcome = new Label
        {
            Text = "欢迎使用 HGmusic",
            Location = new Point(0, 100),
            Size = new Size(720, 44),
            ForeColor = C.TextPrimary,
            BackColor = C.Bg,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Microsoft YaHei UI", 22f, FontStyle.Bold),
        };
        p.Controls.Add(welcome);

        // 副标题
        var subtitle = new Label
        {
            Text = "游戏音效播放助手",
            Location = new Point(0, 150),
            Size = new Size(720, 30),
            ForeColor = C.Primary,
            BackColor = C.Bg,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Microsoft YaHei UI", 12f),
        };
        p.Controls.Add(subtitle);

        // 功能介绍卡片
        var cardPanel = new Panel
        {
            Location = new Point(120, 200),
            Size = new Size(480, 180),
            BackColor = C.Surface,
        };
        cardPanel.Paint += (_, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // 圆角背景
            using var path = CreateRoundPath(cardPanel.ClientRectangle, 12);
            using var brush = new SolidBrush(C.Surface);
            g.FillPath(brush, path);

            // 边框
            using var pen = new Pen(C.Border);
            g.DrawPath(pen, path);
        };
        p.Controls.Add(cardPanel);

        var features = new[]
        {
            "🎮  在游戏中播放自定义音效",
            "🎹  通过快捷键快速触发播放",
            "🎤  将音频注入游戏语音频道",
            "⚡  低延迟、高音质输出",
        };

        for (int i = 0; i < features.Length; i++)
        {
            var featureLabel = new Label
            {
                Text = features[i],
                Location = new Point(30, 20 + i * 40),
                Size = new Size(420, 32),
                ForeColor = C.TextSecondary,
                BackColor = C.Surface,
                Font = new Font("Microsoft YaHei UI", 11f),
            };
            cardPanel.Controls.Add(featureLabel);
        }

        // 提示
        var hint = new Label
        {
            Text = "本向导将帮助您完成初始配置，只需 3 分钟即可开始使用",
            Location = new Point(0, 400),
            Size = new Size(720, 30),
            ForeColor = C.TextMuted,
            BackColor = C.Bg,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Microsoft YaHei UI", 10f),
        };
        p.Controls.Add(hint);
    }

    private void BuildStep1()
    {
        var p = _stepPanels[1];

        // 标题
        var title = new Label
        {
            Text = "📁 导入音频文件",
            Location = new Point(0, 30),
            Size = new Size(720, 40),
            ForeColor = C.TextPrimary,
            BackColor = C.Bg,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Microsoft YaHei UI", 18f, FontStyle.Bold),
        };
        p.Controls.Add(title);

        // 说明
        var hint = new Label
        {
            Text = "选择您喜欢的音效文件，支持多种音频格式",
            Location = new Point(0, 80),
            Size = new Size(720, 28),
            ForeColor = C.TextSecondary,
            BackColor = C.Bg,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Microsoft YaHei UI", 10f),
        };
        p.Controls.Add(hint);

        // 选择按钮
        var btnSelect = new Button
        {
            Text = "📂 选择音频文件",
            Location = new Point(220, 130),
            Size = new Size(280, 50),
            FlatStyle = FlatStyle.Flat,
            BackColor = C.PrimaryLight,
            ForeColor = C.Primary,
            Cursor = Cursors.Hand,
            Font = new Font("Microsoft YaHei UI", 12f, FontStyle.Bold),
        };
        btnSelect.FlatAppearance.BorderColor = C.Primary;
        btnSelect.FlatAppearance.BorderSize = 2;
        btnSelect.FlatAppearance.MouseOverBackColor = Color.FromArgb(220, 220, 255);
        btnSelect.Click += (_, _) =>
        {
            using var ofd = new OpenFileDialog
            {
                Title = "选择音频文件",
                Filter = "音频文件|*.mp3;*.wav;*.aiff;*.aac;*.m4a|所有文件|*.*",
                Multiselect = true,
            };
            if (ofd.ShowDialog(this) == DialogResult.OK)
            {
                foreach (var f in ofd.FileNames)
                {
                    if (!ImportedFiles.Contains(f))
                    {
                        ImportedFiles.Add(f);
                        _fileListBox.Items.Add(Path.GetFileName(f));
                    }
                }
                RefreshFileListHint();
            }
        };
        p.Controls.Add(btnSelect);

        // 格式提示
        var formatLabel = new Label
        {
            Text = "支持格式：MP3、WAV、AIFF、AAC、M4A",
            Location = new Point(0, 190),
            Size = new Size(720, 24),
            ForeColor = C.TextMuted,
            BackColor = C.Bg,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Microsoft YaHei UI", 9f),
        };
        p.Controls.Add(formatLabel);

        // 文件列表
        _fileListBox.Location = new Point(80, 230);
        _fileListBox.Size = new Size(560, 160);
        _fileListBox.BackColor = C.Surface;
        _fileListBox.ForeColor = C.TextPrimary;
        _fileListBox.BorderStyle = BorderStyle.FixedSingle;
        _fileListBox.Font = new Font("Microsoft YaHei UI", 10f);
        p.Controls.Add(_fileListBox);

        // 文件计数标签
        var lblCount = new Label
        {
            Name = "lblCount",
            Text = "尚未选择任何文件",
            Location = new Point(80, 400),
            Size = new Size(560, 28),
            ForeColor = C.TextMuted,
            BackColor = C.Bg,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Microsoft YaHei UI", 10f),
        };
        p.Controls.Add(lblCount);
    }

    private void RefreshFileListHint()
    {
        var lbl = _stepPanels[1].Controls.Find("lblCount", true).FirstOrDefault() as Label;
        if (lbl != null)
        {
            if (ImportedFiles.Count == 0)
            {
                lbl.Text = "尚未选择任何文件";
                lbl.ForeColor = C.TextMuted;
            }
            else
            {
                lbl.Text = $"✓ 已选择 {ImportedFiles.Count} 个文件";
                lbl.ForeColor = C.Success;
            }
        }
    }

    private void BuildStep2()
    {
        var p = _stepPanels[2];

        // 标题
        var title = new Label
        {
            Text = "⌨️ 设置播放热键",
            Location = new Point(0, 30),
            Size = new Size(720, 40),
            ForeColor = C.TextPrimary,
            BackColor = C.Bg,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Microsoft YaHei UI", 18f, FontStyle.Bold),
        };
        p.Controls.Add(title);

        // 说明
        var hint = new Label
        {
            Text = "按下您想要设置的按键组合作为播放触发键",
            Location = new Point(0, 80),
            Size = new Size(720, 28),
            ForeColor = C.TextSecondary,
            BackColor = C.Bg,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Microsoft YaHei UI", 10f),
        };
        p.Controls.Add(hint);

        // 热键输入框
        _keyLabel.Text = "点击此处，然后按下按键...";
        _keyLabel.Location = new Point(160, 130);
        _keyLabel.Size = new Size(400, 70);
        _keyLabel.ForeColor = C.TextMuted;
        _keyLabel.BackColor = C.Surface;
        _keyLabel.TextAlign = ContentAlignment.MiddleCenter;
        _keyLabel.Font = new Font("Microsoft YaHei UI", 16f, FontStyle.Bold);
        _keyLabel.BorderStyle = BorderStyle.FixedSingle;
        _keyLabel.Cursor = Cursors.Hand;
        _keyLabel.Click += (_, _) =>
        {
            _keyLabel.Text = "正在等待按键...";
            _keyLabel.ForeColor = C.Warning;
        };
        p.Controls.Add(_keyLabel);

        // 支持的修饰键说明
        var modsLabel = new Label
        {
            Text = "支持修饰键：Ctrl、Shift、Alt",
            Location = new Point(0, 220),
            Size = new Size(720, 24),
            ForeColor = C.TextMuted,
            BackColor = C.Bg,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Microsoft YaHei UI", 9f),
        };
        p.Controls.Add(modsLabel);

        // 使用说明卡片
        var infoCard = new Panel
        {
            Location = new Point(120, 260),
            Size = new Size(480, 120),
            BackColor = C.Surface,
        };
        infoCard.Paint += (_, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = CreateRoundPath(infoCard.ClientRectangle, 10);
            using var brush = new SolidBrush(C.Surface);
            g.FillPath(brush, path);
            using var pen = new Pen(C.Border);
            g.DrawPath(pen, path);
        };
        p.Controls.Add(infoCard);

        var infoTitle = new Label
        {
            Text = "💡 使用提示",
            Location = new Point(20, 15),
            Size = new Size(440, 28),
            ForeColor = C.Primary,
            BackColor = C.Surface,
            Font = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold),
        };
        infoCard.Controls.Add(infoTitle);

        var infoText = new Label
        {
            Text = "• 此热键将在游戏运行时全局生效\n• 按下后当前选中的音效将播放到队内语音\n• 您可以在主界面中为每个音效单独设置快捷键",
            Location = new Point(20, 50),
            Size = new Size(440, 60),
            ForeColor = C.TextSecondary,
            BackColor = C.Surface,
            Font = new Font("Microsoft YaHei UI", 10f),
        };
        infoCard.Controls.Add(infoText);
    }

    private void BuildStep3()
    {
        var p = _stepPanels[3];

        // 标题
        var title = new Label
        {
            Text = "🎤 配置语音输出",
            Location = new Point(0, 30),
            Size = new Size(720, 40),
            ForeColor = C.TextPrimary,
            BackColor = C.Bg,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Microsoft YaHei UI", 18f, FontStyle.Bold),
        };
        p.Controls.Add(title);

        // 说明
        var hint = new Label
        {
            Text = "通过 VB-Cable 虚拟音频设备将音频注入游戏语音",
            Location = new Point(0, 80),
            Size = new Size(720, 28),
            ForeColor = C.TextSecondary,
            BackColor = C.Bg,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Microsoft YaHei UI", 10f),
        };
        p.Controls.Add(hint);

        // 工作原理卡片
        var howItWorksCard = new Panel
        {
            Location = new Point(80, 120),
            Size = new Size(560, 160),
            BackColor = C.Surface,
        };
        howItWorksCard.Paint += (_, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = CreateRoundPath(howItWorksCard.ClientRectangle, 10);
            using var brush = new SolidBrush(C.Surface);
            g.FillPath(brush, path);
            using var pen = new Pen(C.Border);
            g.DrawPath(pen, path);
        };
        p.Controls.Add(howItWorksCard);

        var howTitle = new Label
        {
            Text = "📡 工作原理",
            Location = new Point(20, 15),
            Size = new Size(520, 28),
            ForeColor = C.Primary,
            BackColor = C.Surface,
            Font = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold),
        };
        howItWorksCard.Controls.Add(howTitle);

        var steps = new[]
        {
            "1. HGmusic 将音频播放到 VB-Cable (CABLE Input)",
            "2. 在游戏中将麦克风设为 CABLE Output",
            "3. 按下热键时，队友就能听到您播放的音效",
        };

        for (int i = 0; i < steps.Length; i++)
        {
            var stepLabel = new Label
            {
                Text = steps[i],
                Location = new Point(20, 50 + i * 32),
                Size = new Size(520, 28),
                ForeColor = C.TextSecondary,
                BackColor = C.Surface,
                Font = new Font("Microsoft YaHei UI", 10f),
            };
            howItWorksCard.Controls.Add(stepLabel);
        }

        // 安装说明卡片
        var installCard = new Panel
        {
            Location = new Point(80, 300),
            Size = new Size(560, 140),
            BackColor = C.PrimaryLight,
        };
        installCard.Paint += (_, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = CreateRoundPath(installCard.ClientRectangle, 10);
            using var brush = new SolidBrush(C.PrimaryLight);
            g.FillPath(brush, path);
            using var pen = new Pen(C.Primary);
            g.DrawPath(pen, path);
        };
        p.Controls.Add(installCard);

        var installTitle = new Label
        {
            Text = "📥 安装 VB-Cable",
            Location = new Point(20, 15),
            Size = new Size(520, 28),
            ForeColor = C.Primary,
            BackColor = C.PrimaryLight,
            Font = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold),
        };
        installCard.Controls.Add(installTitle);

        var installText = new Label
        {
            Text = "1. 访问 https://vb-audio.com/Cable/ 下载安装包\n2. 运行安装程序，按提示完成安装\n3. 安装后在 Windows 声音设置中会出现 CABLE 设备\n4. 在主界面中点击「安装 VB-Cable」按钮可自动安装",
            Location = new Point(20, 50),
            Size = new Size(520, 80),
            ForeColor = C.TextSecondary,
            BackColor = C.PrimaryLight,
            Font = new Font("Microsoft YaHei UI", 10f),
        };
        installCard.Controls.Add(installText);

        // 完成提示
        var finishHint = new Label
        {
            Text = "✓ 完成以上步骤后，点击「完成」即可开始使用",
            Location = new Point(0, 460),
            Size = new Size(720, 28),
            ForeColor = C.Success,
            BackColor = C.Bg,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold),
        };
        p.Controls.Add(finishHint);
    }

    private void OnFormKeyDown(object? sender, KeyEventArgs e)
    {
        if (_currentStep != 2) return;

        var key = e.KeyCode;
        if (key == Keys.Menu || key == Keys.ControlKey || key == Keys.ShiftKey
            || key == Keys.LWin || key == Keys.RWin)
            return;

        _wizardPlayKey = key;
        _wizardPlayModifiers = e.Modifiers;
        _keyLabel.Text = FormatKey(key, e.Modifiers);
        _keyLabel.ForeColor = C.Success;
        e.Handled = true;
    }

    private void OnNextClick(object? sender, EventArgs e)
    {
        if (_currentStep == 3)
        {
            Completed = true;
            Close();
            return;
        }
        GoToStep(_currentStep + 1);
    }

    private void GoToStep(int step)
    {
        if (step < 0 || step > 3) return;
        _stepPanels[_currentStep].Visible = false;
        _currentStep = step;
        _stepPanels[_currentStep].Visible = true;
        UpdateStepDots();
        UpdateNavButtons();
    }

    private void UpdateStepDots()
    {
        for (int i = 0; i < 4; i++)
        {
            if (i < _currentStep)
            {
                // 已完成的步骤
                _stepDots[i].Text = "✓";
                _stepDots[i].BackColor = C.DotCompleted;
                _stepDots[i].ForeColor = C.TextWhite;
            }
            else if (i == _currentStep)
            {
                // 当前步骤
                _stepDots[i].Text = (i + 1).ToString();
                _stepDots[i].BackColor = C.DotActive;
                _stepDots[i].ForeColor = C.TextWhite;
            }
            else
            {
                // 未完成的步骤
                _stepDots[i].Text = (i + 1).ToString();
                _stepDots[i].BackColor = C.DotInactive;
                _stepDots[i].ForeColor = C.TextMuted;
            }
        }
    }

    private void UpdateNavButtons()
    {
        _btnPrev.Visible = _currentStep > 0;

        if (_currentStep == 3)
        {
            _btnNext.Text = "✓ 完成";
            _btnNext.BackColor = C.Success;
            _btnNext.FlatAppearance.MouseOverBackColor = C.SuccessHover;
        }
        else
        {
            _btnNext.Text = "下一步 →";
            _btnNext.BackColor = C.Primary;
            _btnNext.FlatAppearance.MouseOverBackColor = C.PrimaryHover;
        }
    }

    private static string FormatKey(Keys key, Keys mods)
    {
        var parts = new List<string>();
        if ((mods & Keys.Control) != 0) parts.Add("Ctrl");
        if ((mods & Keys.Shift) != 0) parts.Add("Shift");
        if ((mods & Keys.Alt) != 0) parts.Add("Alt");
        if (key != Keys.None) parts.Add(key.ToString());
        if (parts.Count == 0) return "未设置";
        return string.Join(" + ", parts);
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
}
