namespace C__Editor;

public partial class MainEditorForm
{
    private TabControl CreateBottomTabs()
    {
        var bottomTabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Name = "tabBottom",
            SelectedIndex = 0,
            TabIndex = 0
        };

        var buildOutputPage = new TabPage
        {
            Name = "tabPageBuildOutput",
            Text = "\u7F16\u8BD1\u8F93\u51FA",
            Padding = new Padding(3),
            UseVisualStyleBackColor = true
        };

        rtbBuildOutput = new RichTextBox
        {
            Name = "rtbBuildOutput",
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            Font = new Font("Consolas", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0),
            ReadOnly = true
        };
        rtbBuildOutput.ContextMenuStrip = CreateOutputContextMenu(rtbBuildOutput);
        buildOutputPage.Controls.Add(rtbBuildOutput);

        var compileErrorsPage = new TabPage
        {
            Name = "tabPageCompileErrors",
            Text = "\u7F16\u8BD1\u9519\u8BEF\u5217\u8868",
            Padding = new Padding(3),
            UseVisualStyleBackColor = true
        };

        dgvCompileErrors = CreateCompileErrorsGrid();
        compileErrorsPage.Controls.Add(dgvCompileErrors);

        var runOutputPage = new TabPage
        {
            Name = "tabPageRunOutput",
            Text = "\u8FD0\u884C\u7ED3\u679C",
            Padding = new Padding(3),
            UseVisualStyleBackColor = true
        };

        rtbRunOutput = new RichTextBox
        {
            Name = "rtbRunOutput",
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            Font = new Font("Consolas", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0),
            ReadOnly = true
        };
        rtbRunOutput.ContextMenuStrip = CreateOutputContextMenu(rtbRunOutput);
        runOutputPage.Controls.Add(rtbRunOutput);

        bottomTabs.Controls.Add(buildOutputPage);
        bottomTabs.Controls.Add(compileErrorsPage);
        bottomTabs.Controls.Add(runOutputPage);

        return bottomTabs;
    }

    private static ContextMenuStrip CreateOutputContextMenu(RichTextBox targetTextBox)
    {
        var menu = new ContextMenuStrip();

        var menuCopy = new ToolStripMenuItem("复制");
        menuCopy.Click += (_, _) =>
        {
            if (string.IsNullOrEmpty(targetTextBox.SelectedText))
            {
                return;
            }

            try
            {
                Clipboard.SetText(targetTextBox.SelectedText);
            }
            catch
            {
                // Ignore clipboard busy failures.
            }
        };

        var menuCopyAll = new ToolStripMenuItem("复制全部");
        menuCopyAll.Click += (_, _) =>
        {
            if (string.IsNullOrEmpty(targetTextBox.Text))
            {
                return;
            }

            try
            {
                Clipboard.SetText(targetTextBox.Text);
            }
            catch
            {
                // Ignore clipboard busy failures.
            }
        };

        menu.Opening += (_, _) =>
        {
            menuCopy.Enabled = targetTextBox.SelectionLength > 0;
            menuCopyAll.Enabled = targetTextBox.TextLength > 0;
        };

        menu.Items.Add(menuCopy);
        menu.Items.Add(menuCopyAll);
        return menu;
    }

    private DataGridView CreateCompileErrorsGrid()
    {
        var grid = new DataGridView
        {
            Name = "dgvCompileErrors",
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            BackgroundColor = SystemColors.Window,
            MultiSelect = false,
            ReadOnly = true,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.DisplayedCells
        };

        grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grid.ColumnHeadersHeight = 32;
        grid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;

        var columnSeverity = new DataGridViewTextBoxColumn
        {
            Name = "columnSeverity",
            HeaderText = "\u4E25\u91CD\u6027",
            ReadOnly = true,
            Width = 90
        };

        var columnFile = new DataGridViewTextBoxColumn
        {
            Name = "columnFile",
            HeaderText = "\u6587\u4EF6",
            ReadOnly = true,
            Width = 220
        };

        var columnLine = new DataGridViewTextBoxColumn
        {
            Name = "columnLine",
            HeaderText = "\u884C",
            ReadOnly = true,
            Width = 70
        };

        var columnColumn = new DataGridViewTextBoxColumn
        {
            Name = "columnColumn",
            HeaderText = "\u5217",
            ReadOnly = true,
            Width = 70
        };

        var columnErrorCode = new DataGridViewTextBoxColumn
        {
            Name = "columnErrorCode",
            HeaderText = "\u9519\u8BEF\u4EE3\u7801",
            ReadOnly = true,
            Width = 110
        };

        var columnDescription = new DataGridViewTextBoxColumn
        {
            Name = "columnDescription",
            HeaderText = "\u63CF\u8FF0",
            ReadOnly = true,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        };

        grid.Columns.AddRange(
            columnSeverity,
            columnFile,
            columnLine,
            columnColumn,
            columnErrorCode,
            columnDescription);

        return grid;
    }
}
