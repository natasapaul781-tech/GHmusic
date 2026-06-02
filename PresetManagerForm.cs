namespace HGmusic;

public class PresetManagerForm : Form
{
    private readonly ListBox _presetListBox;
    private readonly Button _btnNew;
    private readonly Button _btnCopy;
    private readonly Button _btnRename;
    private readonly Button _btnDelete;
    private readonly Button _btnSwitch;

    public event Action? PresetChanged;

    public PresetManagerForm()
    {
        Text = "预设管理";
        Size = new Size(520, 440);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Theme.Colors.Bg;
        ForeColor = Theme.Colors.TextPrimary;
        Font = Theme.Fonts.Body(10f / 11f);

        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 44,
            BackColor = Theme.Colors.HeaderBg,
        };
        headerPanel.Paint += (_, e) =>
        {
            using var p = new Pen(Theme.Colors.Border);
            e.Graphics.DrawLine(p, 0, headerPanel.Height - 1, headerPanel.Width, headerPanel.Height - 1);
        };

        var titleLabel = new Label
        {
            Text = "选择预设",
            Location = new Point(16, 10),
            Size = new Size(200, 24),
            ForeColor = Theme.Colors.TextPrimary,
            BackColor = Theme.Colors.HeaderBg,
            Font = Theme.Fonts.Subtitle(1f),
        };
        headerPanel.Controls.Add(titleLabel);
        Controls.Add(headerPanel);

        _presetListBox = new ListBox
        {
            Location = new Point(16, 56),
            Size = new Size(488, 240),
            BackColor = Theme.Colors.Surface,
            ForeColor = Theme.Colors.TextPrimary,
            BorderStyle = BorderStyle.FixedSingle,
            Font = Theme.Fonts.Body(10f / 11f),
            IntegralHeight = false,
        };
        _presetListBox.DoubleClick += (_, _) => SwitchToSelectedPreset();
        _presetListBox.DrawMode = DrawMode.OwnerDrawFixed;
        _presetListBox.ItemHeight = 36;
        _presetListBox.DrawItem += OnDrawPresetItem;
        Controls.Add(_presetListBox);

        var btnPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 120,
            BackColor = Theme.Colors.HeaderBg,
        };
        btnPanel.Paint += (_, e) =>
        {
            using var p = new Pen(Theme.Colors.Border);
            e.Graphics.DrawLine(p, 0, 0, btnPanel.Width, 0);
        };
        Controls.Add(btnPanel);

        _btnNew = CreateButton("新建", new Point(16, 14), Theme.Colors.Success);
        _btnNew.Click += (_, _) => CreateNewPreset();
        btnPanel.Controls.Add(_btnNew);

        _btnCopy = CreateButton("复制", new Point(110, 14), Theme.Colors.Accent);
        _btnCopy.Click += (_, _) => CopyPreset();
        btnPanel.Controls.Add(_btnCopy);

        _btnRename = CreateButton("重命名", new Point(204, 14), Theme.Colors.Warning);
        _btnRename.Click += (_, _) => RenamePreset();
        btnPanel.Controls.Add(_btnRename);

        _btnDelete = CreateButton("删除", new Point(308, 14), Theme.Colors.Error);
        _btnDelete.Click += (_, _) => DeleteSelectedPreset();
        btnPanel.Controls.Add(_btnDelete);

        _btnSwitch = new Button
        {
            Text = "切换到此预设",
            Location = new Point(160, 64),
            Size = new Size(200, 38),
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.Colors.Accent,
            ForeColor = Theme.Colors.TextPrimary,
            Cursor = Cursors.Hand,
            Font = Theme.Fonts.BodyBold(10f / 11f),
        };
        _btnSwitch.FlatAppearance.BorderSize = 0;
        _btnSwitch.FlatAppearance.MouseOverBackColor = Theme.Colors.AccentHover;
        _btnSwitch.Click += (_, _) => SwitchToSelectedPreset();
        btnPanel.Controls.Add(_btnSwitch);

        RefreshPresetList();
    }

    private Button CreateButton(string text, Point location, Color color)
    {
        var btn = new Button
        {
            Text = text,
            Location = location,
            Size = new Size(84, 34),
            FlatStyle = FlatStyle.Flat,
            BackColor = color,
            ForeColor = Theme.Colors.TextPrimary,
            Cursor = Cursors.Hand,
            Font = Theme.Fonts.BodyBold(9f / 11f),
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(
            Math.Min(color.R + 30, 255),
            Math.Min(color.G + 30, 255),
            Math.Min(color.B + 30, 255));
        return btn;
    }

    private void RefreshPresetList()
    {
        var presets = AppConfig.GetPresets();
        var current = AppConfig.CurrentPresetName;

        _presetListBox.BeginUpdate();
        _presetListBox.Items.Clear();
        foreach (var preset in presets)
            _presetListBox.Items.Add(preset);

        for (int i = 0; i < _presetListBox.Items.Count; i++)
        {
            if ((string)_presetListBox.Items[i] == current)
            {
                _presetListBox.SelectedIndex = i;
                break;
            }
        }
        _presetListBox.EndUpdate();

        UpdateButtonStates();
    }

    private void UpdateButtonStates()
    {
        var selected = _presetListBox.SelectedItem as string;
        var isCurrent = selected == AppConfig.CurrentPresetName;
        var isLast = _presetListBox.Items.Count <= 1;

        _btnDelete.Enabled = !isCurrent && !isLast;
        _btnDelete.BackColor = (!isCurrent && !isLast) ? Theme.Colors.Error : Theme.Colors.TextMuted;
        _btnSwitch.Enabled = !isCurrent;
        _btnSwitch.BackColor = !isCurrent ? Theme.Colors.Accent : Theme.Colors.TextMuted;
    }

    private void OnDrawPresetItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;

        var text = (string)_presetListBox.Items[e.Index];
        var isCurrent = text == AppConfig.CurrentPresetName;
        var isSelected = (e.State & DrawItemState.Selected) != 0;

        using var bgBrush = new SolidBrush(isSelected ? Theme.Colors.ButtonHover : Theme.Colors.Surface);
        e.Graphics.FillRectangle(bgBrush, e.Bounds);

        var prefix = isCurrent ? Theme.Icons.Play + "  " : "     ";
        var displayText = $"{prefix}{text}";
        var textColor = isCurrent ? Theme.Colors.Success : Theme.Colors.TextPrimary;

        var textRect = new Rectangle(e.Bounds.X + 10, e.Bounds.Y, e.Bounds.Width - 20, e.Bounds.Height);
        using var textBrush = new SolidBrush(textColor);
        var sf = new StringFormat { LineAlignment = StringAlignment.Center };
        e.Graphics.DrawString(displayText, _presetListBox.Font, textBrush, textRect, sf);
    }

    private void CreateNewPreset()
    {
        var name = ShowInputDialog("新建预设", "请输入预设名称：", "");
        if (string.IsNullOrWhiteSpace(name)) return;
        name = name.Trim();

        if (PresetExists(name))
        {
            MessageBox.Show($"预设 \"{name}\" 已存在。", "提示",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        AppConfig.CreatePreset(name);
        AppConfig.SetPreset(name);
        RefreshPresetList();
        PresetChanged?.Invoke();
    }

    private void CopyPreset()
    {
        var selected = _presetListBox.SelectedItem as string;
        if (selected == null) return;

        var name = ShowInputDialog("复制预设", "请输入新预设名称：", $"{selected} (副本)");
        if (string.IsNullOrWhiteSpace(name)) return;
        name = name.Trim();

        if (PresetExists(name))
        {
            MessageBox.Show($"预设 \"{name}\" 已存在。", "提示",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        AppConfig.DuplicatePreset(selected, name);
        RefreshPresetList();
    }

    private void RenamePreset()
    {
        var selected = _presetListBox.SelectedItem as string;
        if (selected == null) return;

        if (selected == "默认预设")
        {
            MessageBox.Show("不能重命名默认预设。", "提示",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var name = ShowInputDialog("重命名预设", "请输入新名称：", selected);
        if (string.IsNullOrWhiteSpace(name) || name.Trim() == selected) return;
        name = name.Trim();

        if (PresetExists(name))
        {
            MessageBox.Show($"预设 \"{name}\" 已存在。", "提示",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        AppConfig.DuplicatePreset(selected, name);
        AppConfig.DeletePreset(selected);

        if (AppConfig.CurrentPresetName == selected)
            AppConfig.SetPreset(name);

        RefreshPresetList();
        PresetChanged?.Invoke();
    }

    private void DeleteSelectedPreset()
    {
        var selected = _presetListBox.SelectedItem as string;
        if (selected == null) return;

        if (selected == AppConfig.CurrentPresetName)
        {
            MessageBox.Show("不能删除当前正在使用的预设。", "提示",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_presetListBox.Items.Count <= 1)
        {
            MessageBox.Show("不能删除最后一个预设。", "提示",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var result = MessageBox.Show($"确定要删除预设 \"{selected}\" 吗？此操作不可撤销。",
            "确认删除", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

        if (result != DialogResult.Yes) return;

        AppConfig.DeletePreset(selected);
        RefreshPresetList();
    }

    private void SwitchToSelectedPreset()
    {
        var selected = _presetListBox.SelectedItem as string;
        if (selected == null) return;
        if (selected == AppConfig.CurrentPresetName) return;

        AppConfig.SetPreset(selected);
        RefreshPresetList();
        PresetChanged?.Invoke();
    }

    private bool PresetExists(string name)
    {
        foreach (string item in _presetListBox.Items)
        {
            if (string.Equals(item, name, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private string? ShowInputDialog(string title, string prompt, string defaultText)
    {
        using var dialog = new Form
        {
            Text = title,
            Size = new Size(380, 170),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            StartPosition = FormStartPosition.CenterParent,
            BackColor = Theme.Colors.Bg,
            ForeColor = Theme.Colors.TextPrimary,
            Font = Theme.Fonts.Body(10f / 11f),
        };

        var promptLabel = new Label
        {
            Text = prompt,
            Location = new Point(16, 16),
            Size = new Size(340, 20),
            ForeColor = Theme.Colors.TextMuted,
            BackColor = Theme.Colors.Bg,
        };
        dialog.Controls.Add(promptLabel);

        var textBox = new TextBox
        {
            Text = defaultText,
            Location = new Point(16, 44),
            Size = new Size(330, 28),
            BackColor = Theme.Colors.Surface,
            ForeColor = Theme.Colors.TextPrimary,
            BorderStyle = BorderStyle.FixedSingle,
            Font = Theme.Fonts.Body(10f / 11f),
        };
        textBox.SelectAll();
        dialog.Controls.Add(textBox);

        var okButton = new Button
        {
            Text = "确定",
            DialogResult = DialogResult.OK,
            Location = new Point(160, 88),
            Size = new Size(90, 32),
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.Colors.Accent,
            ForeColor = Theme.Colors.TextPrimary,
            Cursor = Cursors.Hand,
        };
        okButton.FlatAppearance.BorderSize = 0;
        okButton.FlatAppearance.MouseOverBackColor = Theme.Colors.AccentHover;
        dialog.Controls.Add(okButton);

        var cancelButton = new Button
        {
            Text = "取消",
            DialogResult = DialogResult.Cancel,
            Location = new Point(256, 88),
            Size = new Size(90, 32),
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.Colors.Surface,
            ForeColor = Theme.Colors.TextPrimary,
            Cursor = Cursors.Hand,
        };
        cancelButton.FlatAppearance.BorderSize = 0;
        cancelButton.FlatAppearance.MouseOverBackColor = Theme.Colors.ButtonDefault;
        dialog.Controls.Add(cancelButton);

        dialog.AcceptButton = okButton;
        dialog.CancelButton = cancelButton;

        return dialog.ShowDialog(this) == DialogResult.OK ? textBox.Text.Trim() : null;
    }
}
