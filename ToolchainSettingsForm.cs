namespace C__Editor;

internal sealed class ToolchainSettingsForm : Form
{
    private readonly TextBox txtArchivePath;
    private readonly TextBox txtRootPath;
    private readonly TextBox txtGppPath;
    private readonly TextBox txtGdbPath;
    private readonly TextBox txtCompilerArgs;
    private readonly TextBox txtBuildOutputDirectory;

    internal ToolchainSettingsForm(ToolchainSettingsConfig currentSettings)
    {
        Text = "编译器设置";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(860, 560);
        ClientSize = new Size(980, 620);

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
            ColumnCount = 3,
            RowCount = 8,
            AutoScroll = true
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220f));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110f));

        for (var i = 0; i < 7; i++)
        {
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }

        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        root.Controls.Add(grid, 0, 0);

        txtArchivePath = CreatePathRow(grid, 0, "MinGW 压缩包路径 (.7z)", currentSettings.CompilerArchivePath, BrowseArchivePath);
        txtRootPath = CreatePathRow(grid, 1, "工具链根目录", currentSettings.ToolchainRootPath, BrowseRootPath);
        txtGppPath = CreatePathRow(grid, 2, "g++.exe 路径", currentSettings.GppPath, BrowseGppPath);
        txtGdbPath = CreatePathRow(grid, 3, "gdb.exe 路径", currentSettings.GdbPath, BrowseGdbPath);
        txtCompilerArgs = CreateTextRow(grid, 4, "编译参数", currentSettings.CompilerArguments);
        txtBuildOutputDirectory = CreateTextRow(grid, 5, "输出目录（相对工作区）", currentSettings.BuildOutputDirectory);

        var hint = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ForeColor = Color.DimGray,
            Text =
                "优先级: 手动 g++.exe/gdb.exe -> 工具链根目录 -> 压缩包推断 -> PATH。\r\n" +
                "建议编译参数: -std=c++17 -g"
        };
        grid.Controls.Add(hint, 0, 6);
        grid.SetColumnSpan(hint, 3);

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
    }

    internal ToolchainSettingsConfig ResultSettings => new()
    {
        CompilerArchivePath = txtArchivePath.Text.Trim(),
        ToolchainRootPath = txtRootPath.Text.Trim(),
        GppPath = txtGppPath.Text.Trim(),
        GdbPath = txtGdbPath.Text.Trim(),
        CompilerArguments = txtCompilerArgs.Text.Trim(),
        BuildOutputDirectory = txtBuildOutputDirectory.Text.Trim()
    };

    private static TextBox CreatePathRow(TableLayoutPanel host, int rowIndex, string labelText, string value, EventHandler browseHandler)
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

        var button = new Button
        {
            Text = "浏览...",
            AutoSize = true,
            Margin = new Padding(0, 3, 0, 3)
        };
        button.Click += browseHandler;
        host.Controls.Add(button, 2, rowIndex);

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
        host.SetColumnSpan(textBox, 2);
        return textBox;
    }

    private void BrowseArchivePath(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "选择 MinGW 压缩包",
            CheckFileExists = true,
            Filter = "7z 文件 (*.7z)|*.7z|所有文件 (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            txtArchivePath.Text = dialog.FileName;
        }
    }

    private void BrowseRootPath(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "选择工具链根目录（包含 bin/g++.exe）",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            txtRootPath.Text = dialog.SelectedPath;
        }
    }

    private void BrowseGppPath(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "选择 g++.exe",
            CheckFileExists = true,
            Filter = "g++.exe|g++.exe|可执行文件 (*.exe)|*.exe|所有文件 (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            txtGppPath.Text = dialog.FileName;
        }
    }

    private void BrowseGdbPath(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "选择 gdb.exe",
            CheckFileExists = true,
            Filter = "gdb.exe|gdb.exe|可执行文件 (*.exe)|*.exe|所有文件 (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            txtGdbPath.Text = dialog.FileName;
        }
    }
}
