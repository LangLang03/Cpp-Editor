namespace C__Editor;

internal sealed class ToolchainSettingsForm : Form
{
    private readonly TextBox txtWorkspaceRoot;
    private readonly TextBox txtCompilerPath;
    private readonly TextBox txtToolchainRoot;
    private readonly TextBox txtSetupScript;
    private readonly TextBox txtCompilerArgs;
    private readonly TextBox txtBuildOutputDirectory;
    private readonly TextBox txtCompileList;
    private readonly Button btnRefreshProbe;
    private readonly string debuggerPath;

    private readonly Dictionary<ToolchainId, RadioButton> toolchainRadioById = new();
    private readonly Dictionary<ToolchainId, Label> toolchainStatusById = new();
    private readonly Dictionary<ToolchainId, string> argumentsByToolchain;

    private IReadOnlyList<ToolchainProbeResult> probeResults = Array.Empty<ToolchainProbeResult>();
    private ToolchainId selectedToolchainId;
    private bool isUpdatingSelection;

    internal ToolchainSettingsForm(
        ToolchainSettingsConfig currentSettings,
        string workspaceRootPath,
        IReadOnlyList<string> compileListPatterns)
    {
        Text = "编译器设置";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(960, 700);
        ClientSize = new Size(1080, 760);

        selectedToolchainId = EditorToolchainSettingsController.GetSelectedToolchainId(currentSettings);
        argumentsByToolchain = EditorToolchainSettingsController.GetArgumentsByToolchain(currentSettings);
        EnsureArgumentsMapHasAllKeys(argumentsByToolchain);
        debuggerPath = string.IsNullOrWhiteSpace(currentSettings.DebuggerPath) ? currentSettings.GdbPath : currentSettings.DebuggerPath;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(12)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 9,
            AutoScroll = true
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220f));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 310f));
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(grid, 0, 0);

        txtWorkspaceRoot = CreateReadOnlyRow(grid, 0, "当前工作区根目录", workspaceRootPath);
        var toolchainSelector = CreateToolchainSelectorPanel(out var refreshButton);
        btnRefreshProbe = refreshButton;
        AddControlRow(grid, 1, "工具链（单选）", toolchainSelector);
        txtCompilerPath = CreateReadOnlyRow(grid, 2, "编译器路径", string.Empty);
        txtToolchainRoot = CreateReadOnlyRow(grid, 3, "工具链根目录", string.Empty);
        txtSetupScript = CreateReadOnlyRow(grid, 4, "MSVC 环境脚本", string.Empty);
        txtCompilerArgs = CreateTextRow(grid, 5, "编译参数", string.Empty);
        txtBuildOutputDirectory = CreateTextRow(grid, 6, "输出目录（相对工作区）", currentSettings.BuildOutputDirectory);
        txtCompileList = CreateMultilineRow(
            grid,
            7,
            "编译列表（每行一条）",
            WorkspaceCompileListController.ToMultilineText(compileListPatterns));

        var hint = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ForeColor = Color.DimGray,
            Text =
                "探测顺序：PATH -> 常见目录；内置目录单独检测。\r\n" +
                "不可用工具链会灰显，编译时不自动回退。\r\n" +
                "编译列表支持: path/xx.cpp 与 xx/*.cpp（保存到 .cppeditor/compile-list.json）。"
        };
        grid.Controls.Add(hint, 0, 8);
        grid.SetColumnSpan(hint, 2);

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true
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

        buttonPanel.Controls.Add(btnOk);
        buttonPanel.Controls.Add(btnCancel);
        root.Controls.Add(buttonPanel, 0, 1);

        AcceptButton = btnOk;
        CancelButton = btnCancel;

        btnRefreshProbe.Click += (_, _) => RefreshProbeResults(preserveSelection: true);
        RefreshProbeResults(preserveSelection: false);
    }

    internal ToolchainSettingsConfig ResultSettings
    {
        get
        {
            CommitCurrentArguments();
            var selectedKey = ToolchainCatalog.ToConfigValue(selectedToolchainId);
            var argsMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in argumentsByToolchain)
            {
                argsMap[ToolchainCatalog.ToConfigValue(pair.Key)] = pair.Value;
            }

            return new ToolchainSettingsConfig
            {
                SelectedToolchainId = selectedKey,
                ArgumentsByToolchain = argsMap,
                CompilerArguments = argsMap.TryGetValue(selectedKey, out var selectedArguments)
                    ? selectedArguments
                    : ToolchainCatalog.GetDefaultArguments(selectedToolchainId),
                BuildOutputDirectory = txtBuildOutputDirectory.Text.Trim(),
                CompilerPath = string.Empty,
                SetupScriptPath = string.Empty,
                ToolchainRootPath = string.Empty,
                CompilerArchivePath = string.Empty,
                GppPath = string.Empty,
                DebuggerPath = debuggerPath,
                GdbPath = debuggerPath
            };
        }
    }

    internal IReadOnlyList<string> ResultCompileListPatterns =>
        WorkspaceCompileListController.ParsePatternsFromText(txtCompileList.Text);

    private void RefreshProbeResults(bool preserveSelection)
    {
        if (preserveSelection)
        {
            CommitCurrentArguments();
        }

        probeResults = EditorToolchainSettingsController.DiscoverToolchains();
        foreach (var item in ToolchainCatalog.GetItems())
        {
            var probe = FindProbeResult(item.Id);
            var radio = toolchainRadioById[item.Id];
            var statusLabel = toolchainStatusById[item.Id];

            if (probe?.IsAvailable == true)
            {
                radio.Enabled = true;
                statusLabel.ForeColor = Color.DarkGreen;
                statusLabel.Text = $"{probe.Source}: {probe.CompilerPath}";
            }
            else
            {
                radio.Enabled = false;
                statusLabel.ForeColor = Color.DimGray;
                statusLabel.Text = $"不可用: {probe?.UnavailableReason ?? "未找到"}";
            }
        }

        var hasAvailableSelected = FindProbeResult(selectedToolchainId)?.IsAvailable == true;
        if (!hasAvailableSelected)
        {
            var firstAvailable = probeResults.FirstOrDefault(item => item.IsAvailable);
            if (firstAvailable is not null)
            {
                selectedToolchainId = firstAvailable.Id;
            }
        }

        isUpdatingSelection = true;
        try
        {
            foreach (var pair in toolchainRadioById)
            {
                pair.Value.Checked = pair.Key == selectedToolchainId && pair.Value.Enabled;
            }
        }
        finally
        {
            isUpdatingSelection = false;
        }

        UpdateSelectedToolchainDetails();
    }

    private ToolchainProbeResult? FindProbeResult(ToolchainId id)
    {
        return probeResults.FirstOrDefault(item => item.Id == id);
    }

    private void ToolchainRadio_CheckedChanged(object? sender, EventArgs e)
    {
        if (isUpdatingSelection || sender is not RadioButton radio || !radio.Checked)
        {
            return;
        }

        if (radio.Tag is not ToolchainId id)
        {
            return;
        }

        if (id == selectedToolchainId)
        {
            return;
        }

        CommitCurrentArguments();
        selectedToolchainId = id;
        UpdateSelectedToolchainDetails();
    }

    private void UpdateSelectedToolchainDetails()
    {
        var probe = FindProbeResult(selectedToolchainId);
        if (probe?.IsAvailable == true)
        {
            txtCompilerPath.Text = probe.CompilerPath;
            txtToolchainRoot.Text = probe.ToolchainRootPath;
            txtSetupScript.Text = string.IsNullOrWhiteSpace(probe.SetupScriptPath) ? "(无)" : probe.SetupScriptPath;
        }
        else
        {
            txtCompilerPath.Text = string.Empty;
            txtToolchainRoot.Text = string.Empty;
            txtSetupScript.Text = string.Empty;
        }

        txtCompilerArgs.Text = argumentsByToolchain.TryGetValue(selectedToolchainId, out var args)
            ? args
            : ToolchainCatalog.GetDefaultArguments(selectedToolchainId);
    }

    private void CommitCurrentArguments()
    {
        argumentsByToolchain[selectedToolchainId] = string.IsNullOrWhiteSpace(txtCompilerArgs.Text)
            ? ToolchainCatalog.GetDefaultArguments(selectedToolchainId)
            : txtCompilerArgs.Text.Trim();
    }

    private static void EnsureArgumentsMapHasAllKeys(Dictionary<ToolchainId, string> map)
    {
        foreach (var item in ToolchainCatalog.GetItems())
        {
            if (!map.TryGetValue(item.Id, out var value) || string.IsNullOrWhiteSpace(value))
            {
                map[item.Id] = ToolchainCatalog.GetDefaultArguments(item.Id);
            }
        }
    }

    private Panel CreateToolchainSelectorPanel(out Button refreshButton)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill
        };

        var topBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft
        };

        refreshButton = new Button
        {
            Text = "刷新探测",
            AutoSize = true
        };

        topBar.Controls.Add(refreshButton);
        panel.Controls.Add(topBar);

        var list = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = ToolchainCatalog.GetItems().Count
        };
        list.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180f));
        list.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        var rowIndex = 0;
        foreach (var item in ToolchainCatalog.GetItems())
        {
            list.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var radio = new RadioButton
            {
                AutoSize = true,
                Text = item.DisplayName,
                Tag = item.Id,
                Margin = new Padding(0, 4, 8, 4)
            };
            radio.CheckedChanged += ToolchainRadio_CheckedChanged;

            var status = new Label
            {
                Dock = DockStyle.Fill,
                AutoEllipsis = true,
                Margin = new Padding(0, 7, 0, 4),
                TextAlign = ContentAlignment.MiddleLeft
            };

            toolchainRadioById[item.Id] = radio;
            toolchainStatusById[item.Id] = status;

            list.Controls.Add(radio, 0, rowIndex);
            list.Controls.Add(status, 1, rowIndex);
            rowIndex++;
        }

        panel.Controls.Add(list);
        return panel;
    }

    private static void AddControlRow(TableLayoutPanel host, int rowIndex, string labelText, Control control)
    {
        var label = new Label
        {
            Text = labelText,
            AutoSize = true,
            Margin = new Padding(0, 9, 8, 8)
        };
        host.Controls.Add(label, 0, rowIndex);

        control.Margin = new Padding(0, 4, 8, 4);
        host.Controls.Add(control, 1, rowIndex);
    }

    private static TextBox CreateReadOnlyRow(TableLayoutPanel host, int rowIndex, string labelText, string value)
    {
        var label = new Label
        {
            Text = labelText,
            AutoSize = true,
            Margin = new Padding(0, 9, 8, 8)
        };
        host.Controls.Add(label, 0, rowIndex);

        var textBox = new TextBox
        {
            Text = value ?? string.Empty,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 8, 4),
            ReadOnly = true
        };
        host.Controls.Add(textBox, 1, rowIndex);
        return textBox;
    }

    private static TextBox CreateTextRow(TableLayoutPanel host, int rowIndex, string labelText, string value)
    {
        var label = new Label
        {
            Text = labelText,
            AutoSize = true,
            Margin = new Padding(0, 9, 8, 8)
        };
        host.Controls.Add(label, 0, rowIndex);

        var textBox = new TextBox
        {
            Text = value ?? string.Empty,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 8, 4)
        };
        host.Controls.Add(textBox, 1, rowIndex);
        return textBox;
    }

    private static TextBox CreateMultilineRow(TableLayoutPanel host, int rowIndex, string labelText, string value)
    {
        var label = new Label
        {
            Text = labelText,
            AutoSize = true,
            Margin = new Padding(0, 9, 8, 8)
        };
        host.Controls.Add(label, 0, rowIndex);

        var textBox = new TextBox
        {
            Text = value ?? string.Empty,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 8, 4),
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            AcceptsReturn = true,
            WordWrap = false,
            Height = 160
        };
        host.Controls.Add(textBox, 1, rowIndex);
        return textBox;
    }
}
