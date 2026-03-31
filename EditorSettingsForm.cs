namespace C__Editor;

internal sealed class EditorSettingsForm : Form
{
    private const string ColumnCommandId = "colCommandId";
    private const string ColumnCategory = "colCategory";
    private const string ColumnCommand = "colCommand";
    private const string ColumnGesture = "colGesture";
    private const string ColumnDefault = "colDefault";

    private readonly TreeView treeSettings;
    private readonly Panel panelHost;
    private readonly Panel pageAutoPairs;
    private readonly Panel pageCppTemplates;
    private readonly Panel pageLayout;
    private readonly Panel pageExplorer;
    private readonly Panel pageShortcuts;
    private readonly DataGridView dgvShortcuts;
    private readonly List<ShortcutBindingItem> shortcutBindings;

    private TextBox textAutoPairs = null!;
    private TextBox txtTemplateCpp = null!;
    private TextBox txtTemplateHpp = null!;
    private TextBox txtTemplateC = null!;
    private TextBox txtTemplateH = null!;
    private TextBox txtTemplateOther = null!;
    private CheckBox chkShowProjectTree = null!;
    private CheckBox chkShowOutputPanel = null!;
    private CheckBox chkRestoreLastSession = null!;
    private NumericUpDown numExplorerWidth = null!;
    private ComboBox cmbTheme = null!;
    private CheckBox chkRenameSelectNameOnly = null!;

    private TextBox txtShortcutRecorder = null!;
    private Label lblRecorderHint = null!;
    private Label lblConflictStatus = null!;

    internal EditorSettingsForm(
        string autoPairFormat,
        UiSettings uiSettings,
        ExplorerSettingsConfig explorerSettings,
        CppTemplateSettingsConfig cppTemplateSettings,
        IReadOnlyList<ShortcutBindingItem> shortcutItems)
    {
        shortcutBindings = shortcutItems.Select(item => item.Clone()).ToList();

        Text = "设置";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(900, 620);
        ClientSize = new Size(1040, 700);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterDistance = 250,
            FixedPanel = FixedPanel.Panel1
        };
        Controls.Add(split);

        treeSettings = new TreeView
        {
            Dock = DockStyle.Fill,
            HideSelection = false
        };
        split.Panel1.Controls.Add(treeSettings);

        var rootEditor = new TreeNode("编辑器");
        rootEditor.Nodes.Add(new TreeNode("自动补全") { Name = "auto_pairs" });
        rootEditor.Nodes.Add(new TreeNode("C++") { Name = "cpp_templates" });
        rootEditor.Nodes.Add(new TreeNode("快捷键") { Name = "shortcuts" });
        var rootWorkspace = new TreeNode("工作区");
        rootWorkspace.Nodes.Add(new TreeNode("布局") { Name = "layout" });
        rootWorkspace.Nodes.Add(new TreeNode("资源管理器") { Name = "explorer" });
        treeSettings.Nodes.Add(rootEditor);
        treeSettings.Nodes.Add(rootWorkspace);
        treeSettings.ExpandAll();
        treeSettings.AfterSelect += TreeSettings_AfterSelect;

        var rightContainer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        rightContainer.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        rightContainer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        split.Panel2.Controls.Add(rightContainer);

        panelHost = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14, 12, 14, 12)
        };
        rightContainer.Controls.Add(panelHost, 0, 0);

        pageAutoPairs = new Panel { Dock = DockStyle.Fill };
        pageCppTemplates = new Panel { Dock = DockStyle.Fill };
        pageLayout = new Panel { Dock = DockStyle.Fill };
        pageExplorer = new Panel { Dock = DockStyle.Fill };
        pageShortcuts = new Panel { Dock = DockStyle.Fill };
        panelHost.Controls.Add(pageAutoPairs);
        panelHost.Controls.Add(pageCppTemplates);
        panelHost.Controls.Add(pageLayout);
        panelHost.Controls.Add(pageExplorer);
        panelHost.Controls.Add(pageShortcuts);

        BuildAutoPairPage(pageAutoPairs, autoPairFormat);
        BuildCppTemplatePage(pageCppTemplates, cppTemplateSettings);
        BuildLayoutPage(pageLayout, uiSettings);
        BuildExplorerPage(pageExplorer, explorerSettings);
        dgvShortcuts = BuildShortcutPage(pageShortcuts);

        var bottomButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8)
        };

        var btnOk = new Button
        {
            Text = "确定",
            AutoSize = true,
            DialogResult = DialogResult.OK
        };
        var btnCancel = new Button
        {
            Text = "取消",
            AutoSize = true,
            DialogResult = DialogResult.Cancel
        };

        bottomButtons.Controls.Add(btnOk);
        bottomButtons.Controls.Add(btnCancel);
        rightContainer.Controls.Add(bottomButtons, 0, 1);

        AcceptButton = btnOk;
        CancelButton = btnCancel;
        FormClosing += EditorSettingsForm_FormClosing;

        treeSettings.SelectedNode = treeSettings.Nodes[0].Nodes[0];
        RefreshShortcutConflicts();
    }

    internal string AutoPairFormat => textAutoPairs.Text;

    internal UiSettings ResultUiSettings => new()
    {
        ShowProjectTree = chkShowProjectTree.Checked,
        ShowOutputPanel = chkShowOutputPanel.Checked,
        ExplorerWidth = (int)numExplorerWidth.Value,
        RestoreLastSessionOnStartup = chkRestoreLastSession.Checked,
        ThemeId = cmbTheme.SelectedIndex == 1
            ? EditorThemeController.DarkThemeId
            : EditorThemeController.LightThemeId
    };

    internal ExplorerSettingsConfig ResultExplorerSettings => new()
    {
        RenameSelectNameOnly = chkRenameSelectNameOnly.Checked
    };

    internal CppTemplateSettingsConfig ResultCppTemplateSettings => new()
    {
        CppSourceTemplate = NormalizeTemplateText(txtTemplateCpp.Text, CppTemplateSettingsConfig.DefaultCppSourceTemplate),
        CppHeaderTemplate = NormalizeTemplateText(txtTemplateHpp.Text, CppTemplateSettingsConfig.DefaultCppHeaderTemplate),
        CSourceTemplate = NormalizeTemplateText(txtTemplateC.Text, CppTemplateSettingsConfig.DefaultCSourceTemplate),
        CHeaderTemplate = NormalizeTemplateText(txtTemplateH.Text, CppTemplateSettingsConfig.DefaultCHeaderTemplate),
        OtherFileTemplate = NormalizeTemplateText(txtTemplateOther.Text, string.Empty)
    };

    internal IReadOnlyList<ShortcutBindingItem> ResultShortcutBindings => shortcutBindings.Select(item => item.Clone()).ToList();

    private void BuildAutoPairPage(Control host, string autoPairFormat)
    {
        var title = new Label
        {
            Text = "自动补全符号",
            Font = new Font("Microsoft YaHei UI", 12f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(0, 0)
        };

        var desc = new Label
        {
            Text = "按开闭顺序填写，例如：<>{}()[]\"\"''",
            AutoSize = true,
            Location = new Point(0, 34)
        };

        textAutoPairs = new TextBox
        {
            Width = 360,
            Text = autoPairFormat,
            Location = new Point(0, 64)
        };

        var hint = new Label
        {
            Text = "提示：格式必须是偶数长度，按两两成对解析。",
            AutoSize = true,
            ForeColor = Color.DimGray,
            Location = new Point(0, 96)
        };

        host.Controls.Add(title);
        host.Controls.Add(desc);
        host.Controls.Add(textAutoPairs);
        host.Controls.Add(hint);
    }

    private void BuildCppTemplatePage(Control host, CppTemplateSettingsConfig cppTemplateSettings)
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        host.Controls.Add(layout);

        var title = new Label
        {
            Text = "C++ 文件模板",
            Font = new Font("Microsoft YaHei UI", 12f, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 8)
        };
        layout.Controls.Add(title, 0, 0);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 260
        };
        layout.Controls.Add(split, 0, 1);

        var topGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2
        };
        topGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        topGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        topGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
        topGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));

        txtTemplateCpp = CreateTemplateEditor(topGrid, 0, 0, ".cpp 模板", cppTemplateSettings.CppSourceTemplate);
        txtTemplateHpp = CreateTemplateEditor(topGrid, 1, 0, ".hpp 模板", cppTemplateSettings.CppHeaderTemplate);
        txtTemplateC = CreateTemplateEditor(topGrid, 0, 1, ".c 模板", cppTemplateSettings.CSourceTemplate);
        txtTemplateH = CreateTemplateEditor(topGrid, 1, 1, ".h 模板", cppTemplateSettings.CHeaderTemplate);
        split.Panel1.Controls.Add(topGrid);

        txtTemplateOther = CreateTemplateEditor(split.Panel2, ".txt/其他文件模板", cppTemplateSettings.OtherFileTemplate);
    }

    private void BuildExplorerPage(Control host, ExplorerSettingsConfig explorerSettings)
    {
        var title = new Label
        {
            Text = "资源管理器",
            Font = new Font("Microsoft YaHei UI", 12f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(0, 0)
        };

        chkRenameSelectNameOnly = new CheckBox
        {
            Text = "重命名时默认只选中文件名（不含扩展名）",
            Checked = explorerSettings.RenameSelectNameOnly,
            AutoSize = true,
            Location = new Point(0, 40)
        };

        var hint = new Label
        {
            Text = "关闭后将选中整个名称（含扩展名）。",
            AutoSize = true,
            ForeColor = Color.DimGray,
            Location = new Point(0, 70)
        };

        host.Controls.Add(title);
        host.Controls.Add(chkRenameSelectNameOnly);
        host.Controls.Add(hint);
    }

    private void BuildLayoutPage(Control host, UiSettings uiSettings)
    {
        var normalizedThemeId = EditorThemeController.NormalizeThemeId(uiSettings.ThemeId);

        var title = new Label
        {
            Text = "布局",
            Font = new Font("Microsoft YaHei UI", 12f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(0, 0)
        };

        var themeLabel = new Label
        {
            Text = "主题：",
            AutoSize = true,
            Location = new Point(0, 40)
        };

        cmbTheme = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(0, 66),
            Width = 180
        };
        cmbTheme.Items.Add("亮色");
        cmbTheme.Items.Add("暗色");
        cmbTheme.SelectedIndex = string.Equals(normalizedThemeId, EditorThemeController.DarkThemeId, StringComparison.Ordinal)
            ? 1
            : 0;

        chkShowProjectTree = new CheckBox
        {
            Text = "显示资源管理器",
            Checked = uiSettings.ShowProjectTree,
            AutoSize = true,
            Location = new Point(0, 104)
        };

        chkShowOutputPanel = new CheckBox
        {
            Text = "显示底部输出窗口",
            Checked = uiSettings.ShowOutputPanel,
            AutoSize = true,
            Location = new Point(0, 132)
        };

        chkRestoreLastSession = new CheckBox
        {
            Text = "启动时恢复上次会话（双击启动时）",
            Checked = uiSettings.RestoreLastSessionOnStartup,
            AutoSize = true,
            Location = new Point(0, 160)
        };

        var widthLabel = new Label
        {
            Text = "资源管理器宽度（像素）：",
            AutoSize = true,
            Location = new Point(0, 198)
        };

        numExplorerWidth = new NumericUpDown
        {
            Minimum = 180,
            Maximum = 420,
            Value = Math.Clamp(uiSettings.ExplorerWidth, 180, 420),
            Location = new Point(0, 224),
            Width = 120
        };

        host.Controls.Add(title);
        host.Controls.Add(themeLabel);
        host.Controls.Add(cmbTheme);
        host.Controls.Add(chkShowProjectTree);
        host.Controls.Add(chkShowOutputPanel);
        host.Controls.Add(chkRestoreLastSession);
        host.Controls.Add(widthLabel);
        host.Controls.Add(numExplorerWidth);
    }

    private DataGridView BuildShortcutPage(Control host)
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        host.Controls.Add(layout);

        var title = new Label
        {
            Text = "快捷键绑定",
            Font = new Font("Microsoft YaHei UI", 12f, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 8)
        };
        layout.Controls.Add(title, 0, 0);

        var hint = new Label
        {
            Text = "格式示例：Ctrl+S、Ctrl+Shift+O、F5、Alt+Enter。留空表示禁用。",
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 8)
        };
        layout.Controls.Add(hint, 0, 1);

        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            MultiSelect = false,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect
        };

        var colId = new DataGridViewTextBoxColumn
        {
            Name = ColumnCommandId,
            HeaderText = "CommandId",
            Visible = false
        };
        var colCategory = new DataGridViewTextBoxColumn
        {
            Name = ColumnCategory,
            HeaderText = "分类",
            ReadOnly = true,
            Width = 110
        };
        var colCommand = new DataGridViewTextBoxColumn
        {
            Name = ColumnCommand,
            HeaderText = "命令",
            ReadOnly = true,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        };
        var colGesture = new DataGridViewTextBoxColumn
        {
            Name = ColumnGesture,
            HeaderText = "快捷键",
            Width = 180
        };
        var colDefault = new DataGridViewTextBoxColumn
        {
            Name = ColumnDefault,
            HeaderText = "默认",
            ReadOnly = true,
            Width = 150
        };

        grid.Columns.AddRange(colId, colCategory, colCommand, colGesture, colDefault);
        foreach (var item in shortcutBindings)
        {
            grid.Rows.Add(item.CommandId, item.Category, item.CommandName, item.Gesture, item.DefaultGesture);
        }

        grid.CellEndEdit += DgvShortcuts_CellEndEdit;
        grid.CellValueChanged += DgvShortcuts_CellValueChanged;
        grid.SelectionChanged += DgvShortcuts_SelectionChanged;
        layout.Controls.Add(grid, 0, 2);

        var recordPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 6, 0, 0)
        };

        var lblRecord = new Label
        {
            Text = "录制输入:",
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 8, 6, 0)
        };

        txtShortcutRecorder = new TextBox
        {
            Width = 200,
            ReadOnly = true,
            TabStop = true
        };
        txtShortcutRecorder.PreviewKeyDown += TxtShortcutRecorder_PreviewKeyDown;
        txtShortcutRecorder.KeyDown += TxtShortcutRecorder_KeyDown;

        var btnApplyRecorded = new Button
        {
            Text = "应用到选中命令",
            AutoSize = true
        };
        btnApplyRecorded.Click += (_, _) => ApplyRecordedGestureToSelectedRow();

        var btnClearSelected = new Button
        {
            Text = "清空选中命令",
            AutoSize = true
        };
        btnClearSelected.Click += (_, _) => ClearSelectedRowGesture();

        recordPanel.Controls.Add(lblRecord);
        recordPanel.Controls.Add(txtShortcutRecorder);
        recordPanel.Controls.Add(btnApplyRecorded);
        recordPanel.Controls.Add(btnClearSelected);
        layout.Controls.Add(recordPanel, 0, 3);

        var actionPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 6, 0, 0)
        };

        var btnResetDefaults = new Button
        {
            Text = "恢复默认快捷键",
            AutoSize = true
        };
        btnResetDefaults.Click += (_, _) =>
        {
            ResetShortcutRowsToDefaults(grid);
            RefreshShortcutConflicts();
        };

        lblRecorderHint = new Label
        {
            Text = "提示：点击录制框后按下组合键，再点“应用到选中命令”。",
            AutoSize = true,
            ForeColor = Color.DimGray,
            Margin = new Padding(12, 8, 0, 0)
        };

        actionPanel.Controls.Add(btnResetDefaults);
        actionPanel.Controls.Add(lblRecorderHint);
        layout.Controls.Add(actionPanel, 0, 4);

        lblConflictStatus = new Label
        {
            Text = string.Empty,
            AutoSize = true,
            ForeColor = Color.DarkRed,
            Margin = new Padding(0, 6, 0, 0)
        };
        layout.Controls.Add(lblConflictStatus, 0, 5);

        return grid;
    }

    private static void ResetShortcutRowsToDefaults(DataGridView grid)
    {
        foreach (DataGridViewRow row in grid.Rows)
        {
            var defaultValue = row.Cells[ColumnDefault].Value?.ToString() ?? string.Empty;
            row.Cells[ColumnGesture].Value = defaultValue;
        }
    }

    private void DgvShortcuts_CellEndEdit(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0)
        {
            return;
        }

        if (dgvShortcuts.Columns[e.ColumnIndex].Name != ColumnGesture)
        {
            return;
        }

        var row = dgvShortcuts.Rows[e.RowIndex];
        var rawText = row.Cells[ColumnGesture].Value?.ToString() ?? string.Empty;
        if (!EditorShortcutKeyFormatter.TryParse(rawText, out var keys))
        {
            return;
        }

        row.Cells[ColumnGesture].Value = EditorShortcutKeyFormatter.ToDisplayString(keys);
        RefreshShortcutConflicts();
    }

    private void DgvShortcuts_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0)
        {
            return;
        }

        if (dgvShortcuts.Columns[e.ColumnIndex].Name != ColumnGesture)
        {
            return;
        }

        RefreshShortcutConflicts();
    }

    private void DgvShortcuts_SelectionChanged(object? sender, EventArgs e)
    {
        if (dgvShortcuts.SelectedRows.Count == 0)
        {
            return;
        }

        var selectedText = dgvShortcuts.SelectedRows[0].Cells[ColumnGesture].Value?.ToString() ?? string.Empty;
        txtShortcutRecorder.Text = selectedText;
    }

    private void TxtShortcutRecorder_PreviewKeyDown(object? sender, PreviewKeyDownEventArgs e)
    {
        e.IsInputKey = true;
    }

    private void TxtShortcutRecorder_KeyDown(object? sender, KeyEventArgs e)
    {
        var keyData = EditorShortcutKeyFormatter.Normalize(e.KeyData);
        var keyCode = keyData & Keys.KeyCode;
        if (keyCode == Keys.ControlKey || keyCode == Keys.ShiftKey || keyCode == Keys.Menu)
        {
            lblRecorderHint.Text = "请至少包含一个非修饰键（如字母、F5、Delete）。";
            e.SuppressKeyPress = true;
            e.Handled = true;
            return;
        }

        var gesture = EditorShortcutKeyFormatter.ToDisplayString(keyData);
        txtShortcutRecorder.Text = gesture;
        lblRecorderHint.Text = $"已录制: {gesture}";

        e.SuppressKeyPress = true;
        e.Handled = true;
    }

    private void ApplyRecordedGestureToSelectedRow()
    {
        if (dgvShortcuts.SelectedRows.Count == 0)
        {
            MessageBox.Show(this, "请先在表格中选中一个命令。", "快捷键录制", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var recorded = txtShortcutRecorder.Text.Trim();
        if (!EditorShortcutKeyFormatter.TryParse(recorded, out var keys))
        {
            MessageBox.Show(this, "录制结果不是有效快捷键，请重新录制。", "快捷键录制", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var normalized = EditorShortcutKeyFormatter.ToDisplayString(keys);
        var row = dgvShortcuts.SelectedRows[0];
        row.Cells[ColumnGesture].Value = normalized;
        RefreshShortcutConflicts();
    }

    private void ClearSelectedRowGesture()
    {
        if (dgvShortcuts.SelectedRows.Count == 0)
        {
            MessageBox.Show(this, "请先在表格中选中一个命令。", "快捷键设置", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        dgvShortcuts.SelectedRows[0].Cells[ColumnGesture].Value = string.Empty;
        txtShortcutRecorder.Text = string.Empty;
        lblRecorderHint.Text = "已清空选中命令的快捷键。";
        RefreshShortcutConflicts();
    }

    private bool RefreshShortcutConflicts()
    {
        foreach (DataGridViewRow row in dgvShortcuts.Rows)
        {
            var cell = row.Cells[ColumnGesture];
            cell.Style.BackColor = Color.White;
            cell.Style.ForeColor = Color.Black;
            cell.ErrorText = string.Empty;
        }

        var conflicts = new Dictionary<string, List<DataGridViewRow>>(StringComparer.OrdinalIgnoreCase);
        foreach (DataGridViewRow row in dgvShortcuts.Rows)
        {
            var raw = row.Cells[ColumnGesture].Value?.ToString() ?? string.Empty;
            if (!EditorShortcutKeyFormatter.TryParse(raw, out var keys))
            {
                continue;
            }

            var normalized = EditorShortcutKeyFormatter.ToDisplayString(keys);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                continue;
            }

            if (!conflicts.TryGetValue(normalized, out var rows))
            {
                rows = new List<DataGridViewRow>();
                conflicts[normalized] = rows;
            }

            rows.Add(row);
        }

        var conflictPairs = conflicts.Where(pair => pair.Value.Count > 1).ToList();
        if (conflictPairs.Count == 0)
        {
            lblConflictStatus.Text = "未检测到快捷键冲突。";
            lblConflictStatus.ForeColor = Color.DarkGreen;
            return false;
        }

        foreach (var conflict in conflictPairs)
        {
            foreach (var row in conflict.Value)
            {
                var cell = row.Cells[ColumnGesture];
                cell.Style.BackColor = Color.MistyRose;
                cell.Style.ForeColor = Color.DarkRed;
                cell.ErrorText = $"与其他命令重复: {conflict.Key}";
            }
        }

        lblConflictStatus.Text = $"检测到 {conflictPairs.Count} 组冲突，请修复后再保存。";
        lblConflictStatus.ForeColor = Color.DarkRed;
        return true;
    }

    private static TextBox CreateTemplateEditor(Control host, string title, string content)
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        host.Controls.Add(layout);

        var label = new Label
        {
            Text = title,
            Dock = DockStyle.Fill,
            AutoSize = true
        };
        layout.Controls.Add(label, 0, 0);

        var textBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            AcceptsReturn = true,
            AcceptsTab = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Font = new Font("Consolas", 10f, FontStyle.Regular),
            Text = content ?? string.Empty
        };
        layout.Controls.Add(textBox, 0, 1);

        return textBox;
    }

    private static TextBox CreateTemplateEditor(TableLayoutPanel host, int column, int row, string title, string content)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 8, 8)
        };
        host.Controls.Add(panel, column, row);
        return CreateTemplateEditor(panel, title, content);
    }

    private static string NormalizeTemplateText(string text, string fallbackWhenNullOrEmpty)
    {
        if (string.IsNullOrEmpty(text))
        {
            return fallbackWhenNullOrEmpty;
        }

        return text.Replace("\r\n", "\n").Replace('\r', '\n');
    }

    private void TreeSettings_AfterSelect(object? sender, TreeViewEventArgs e)
    {
        pageAutoPairs.Visible = e.Node?.Name == "auto_pairs";
        pageCppTemplates.Visible = e.Node?.Name == "cpp_templates";
        pageLayout.Visible = e.Node?.Name == "layout";
        pageExplorer.Visible = e.Node?.Name == "explorer";
        pageShortcuts.Visible = e.Node?.Name == "shortcuts";
    }

    private void EditorSettingsForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (DialogResult != DialogResult.OK)
        {
            return;
        }

        if (!TryCaptureShortcutBindings())
        {
            e.Cancel = true;
        }
    }

    private bool TryCaptureShortcutBindings()
    {
        dgvShortcuts.EndEdit();

        var pending = new List<ShortcutBindingItem>();
        var duplicateMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (DataGridViewRow row in dgvShortcuts.Rows)
        {
            var commandId = row.Cells[ColumnCommandId].Value?.ToString() ?? string.Empty;
            var category = row.Cells[ColumnCategory].Value?.ToString() ?? string.Empty;
            var commandName = row.Cells[ColumnCommand].Value?.ToString() ?? string.Empty;
            var gestureText = row.Cells[ColumnGesture].Value?.ToString() ?? string.Empty;
            var defaultGesture = row.Cells[ColumnDefault].Value?.ToString() ?? string.Empty;

            if (!EditorShortcutKeyFormatter.TryParse(gestureText, out var keys))
            {
                MessageBox.Show(this, $"“{commandName}” 的快捷键格式无效: {gestureText}", "快捷键设置", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                treeSettings.SelectedNode = FindSettingsNodeByName("shortcuts");
                dgvShortcuts.CurrentCell = row.Cells[ColumnGesture];
                dgvShortcuts.BeginEdit(true);
                return false;
            }

            var normalizedGesture = EditorShortcutKeyFormatter.ToDisplayString(keys);
            row.Cells[ColumnGesture].Value = normalizedGesture;

            if (!string.IsNullOrWhiteSpace(normalizedGesture))
            {
                if (!duplicateMap.TryGetValue(normalizedGesture, out var names))
                {
                    names = new List<string>();
                    duplicateMap[normalizedGesture] = names;
                }

                names.Add(commandName);
            }

            pending.Add(new ShortcutBindingItem
            {
                CommandId = commandId,
                Category = category,
                CommandName = commandName,
                Gesture = normalizedGesture,
                DefaultGesture = defaultGesture
            });
        }

        var conflicts = duplicateMap.Where(pair => pair.Value.Count > 1).ToList();
        if (conflicts.Count > 0)
        {
            RefreshShortcutConflicts();
            var conflictPreview = string.Join("\r\n", conflicts.Select(pair => $"{pair.Key}: {string.Join("、", pair.Value)}"));
            MessageBox.Show(this, $"检测到快捷键冲突，请先修复后再保存：\r\n{conflictPreview}", "快捷键冲突", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            treeSettings.SelectedNode = FindSettingsNodeByName("shortcuts");
            return false;
        }

        shortcutBindings.Clear();
        shortcutBindings.AddRange(pending);
        return true;
    }

    private TreeNode? FindSettingsNodeByName(string nodeName)
    {
        foreach (TreeNode root in treeSettings.Nodes)
        {
            var found = FindNodeByNameRecursive(root, nodeName);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static TreeNode? FindNodeByNameRecursive(TreeNode node, string nodeName)
    {
        if (string.Equals(node.Name, nodeName, StringComparison.Ordinal))
        {
            return node;
        }

        foreach (TreeNode child in node.Nodes)
        {
            var found = FindNodeByNameRecursive(child, nodeName);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }
}
