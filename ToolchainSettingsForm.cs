namespace C__Editor;

internal sealed class ToolchainSettingsForm : Form
{
    private readonly TextBox txtWorkspaceRoot;
    private readonly TextBox txtToolchainRoot;
    private readonly TextBox txtSetupScript;
    private readonly TextBox txtCompilerPath;
    private readonly TextBox txtCompilerArgs;
    private readonly TextBox txtBuildOutputDirectory;
    private readonly TextBox txtCompileList;

    internal ToolchainSettingsForm(
        ToolchainSettingsConfig currentSettings,
        string workspaceRootPath,
        IReadOnlyList<string> compileListPatterns)
    {
        Text = "MSVC 编译器设置";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(860, 620);
        ClientSize = new Size(980, 680);

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

        for (var i = 0; i < 7; i++)
        {
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }

        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.Controls.Add(grid, 0, 0);

        txtWorkspaceRoot = CreateReadOnlyRow(grid, 0, "当前工作区根目录", workspaceRootPath);
        txtToolchainRoot = CreateReadOnlyRow(grid, 1, "内置 MSVC 根目录", currentSettings.ToolchainRootPath);
        txtSetupScript = CreateReadOnlyRow(grid, 2, "环境脚本 (vcvars64.bat)", currentSettings.SetupScriptPath);
        txtCompilerPath = CreateReadOnlyRow(grid, 3, "编译器 (cl.exe)", currentSettings.CompilerPath);
        txtCompilerArgs = CreateTextRow(grid, 4, "编译参数", currentSettings.CompilerArguments);
        txtBuildOutputDirectory = CreateTextRow(grid, 5, "输出目录（相对工作区）", currentSettings.BuildOutputDirectory);

        txtCompileList = CreateMultilineRow(
            grid,
            6,
            "编译列表（每行一条）",
            WorkspaceCompileListController.ToMultilineText(compileListPatterns));

        var hint = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ForeColor = Color.DimGray,
            Text =
                "当前版本固定使用项目内置 msvc 文件夹，不再使用 MinGW。\r\n" +
                "编译列表支持: path/xx.cpp 和 xx/*.cpp（保存到 .cppeditor/compile-list.json）。\r\n" +
                "建议参数: /std:c++17 /EHsc /Zi /nologo"
        };
        grid.Controls.Add(hint, 0, 7);
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
    }

    internal ToolchainSettingsConfig ResultSettings => new()
    {
        ToolchainRootPath = txtToolchainRoot.Text.Trim(),
        SetupScriptPath = txtSetupScript.Text.Trim(),
        CompilerPath = txtCompilerPath.Text.Trim(),
        CompilerArguments = txtCompilerArgs.Text.Trim(),
        BuildOutputDirectory = txtBuildOutputDirectory.Text.Trim(),

        // legacy MinGW fields cleared
        CompilerArchivePath = string.Empty,
        GppPath = string.Empty,
        GdbPath = string.Empty
    };

    internal IReadOnlyList<string> ResultCompileListPatterns =>
        WorkspaceCompileListController.ParsePatternsFromText(txtCompileList.Text);

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
            Height = 140
        };
        host.Controls.Add(textBox, 1, rowIndex);
        return textBox;
    }
}
