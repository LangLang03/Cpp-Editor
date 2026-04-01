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
            TabIndex = 0,
            DrawMode = TabDrawMode.OwnerDrawFixed,
            SizeMode = TabSizeMode.Fixed,
            ItemSize = new Size(150, 32),
            Padding = new Point(12, 6)
        };
        bottomTabs.DrawItem += TabBottom_DrawItem;

        var buildOutputPage = new TabPage
        {
            Name = "tabPageBuildOutput",
            Text = "编译输出",
            Padding = new Padding(0),
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
            Text = "编译错误列表",
            Padding = new Padding(0),
            UseVisualStyleBackColor = true
        };

        dgvCompileErrors = CreateCompileErrorsGrid();
        compileErrorsPage.Controls.Add(dgvCompileErrors);

        var runOutputPage = new TabPage
        {
            Name = "tabPageRunOutput",
            Text = "运行结果",
            Padding = new Padding(0),
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

        var debugVariablesPage = new TabPage
        {
            Name = "tabPageDebugVariables",
            Text = "调试变量",
            Padding = new Padding(0),
            UseVisualStyleBackColor = true
        };

        dgvDebugVariables = CreateDebugVariablesGrid();
        debugVariablesPage.Controls.Add(dgvDebugVariables);

        var runtimeLogPage = new TabPage
        {
            Name = "tabPageRuntimeLog",
            Text = "运行日志",
            Padding = new Padding(0),
            UseVisualStyleBackColor = true
        };

        rtbRuntimeLog = new RichTextBox
        {
            Name = "rtbRuntimeLog",
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            Font = new Font("Consolas", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0),
            ReadOnly = true
        };
        rtbRuntimeLog.ContextMenuStrip = CreateOutputContextMenu(rtbRuntimeLog);
        runtimeLogPage.Controls.Add(rtbRuntimeLog);

        bottomTabs.Controls.Add(buildOutputPage);
        bottomTabs.Controls.Add(compileErrorsPage);
        bottomTabs.Controls.Add(runOutputPage);
        bottomTabs.Controls.Add(debugVariablesPage);
        bottomTabs.Controls.Add(runtimeLogPage);

        tabBottom = bottomTabs; // 很重要

        return bottomTabs;
    }

    private void TabBottom_DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (tabBottom is null || e.Index < 0 || e.Index >= tabBottom.TabPages.Count)
        {
            return;
        }

        var tab = tabBottom.TabPages[e.Index];
        var bounds = e.Bounds;
        var selected = e.Index == tabBottom.SelectedIndex;
        var selectedBackColor = tab.BackColor == Color.Empty ? tabBottom.BackColor : tab.BackColor;
        var unselectedBackColor = BlendColor(tabBottom.BackColor, selectedBackColor, 0.55f);
        var backColor = selected ? selectedBackColor : unselectedBackColor;
        var borderColor = splitMain.BackColor;

        using (var backgroundBrush = new SolidBrush(backColor))
        {
            e.Graphics.FillRectangle(backgroundBrush, bounds);
        }

        var textBounds = new Rectangle(bounds.X + 4, bounds.Y + 2, Math.Max(10, bounds.Width - 8), bounds.Height - 4);
        TextRenderer.DrawText(
            e.Graphics,
            tab.Text,
            tabBottom.Font,
            textBounds,
            tabBottom.ForeColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        using var borderPen = new Pen(borderColor);
        e.Graphics.DrawRectangle(borderPen, bounds);
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
            MultiSelect = true,
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

        grid.ContextMenuStrip = CreateCompileErrorsContextMenu(grid);
        grid.CellDoubleClick += DgvCompileErrors_CellDoubleClick;
        grid.MouseDown += DgvCompileErrors_MouseDown;

        return grid;
    }

    private DataGridView CreateDebugVariablesGrid()
    {
        var grid = new DataGridView
        {
            Name = "dgvDebugVariables",
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

        var columnName = new DataGridViewTextBoxColumn
        {
            Name = "columnVariableName",
            HeaderText = "名称",
            ReadOnly = true,
            Width = 260
        };

        var columnValue = new DataGridViewTextBoxColumn
        {
            Name = "columnVariableValue",
            HeaderText = "值",
            ReadOnly = true,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        };

        grid.Columns.AddRange(columnName, columnValue);
        return grid;
    }

    private void UpdateDebugVariablesGrid(IReadOnlyList<DebugVariableValue> variables)
    {
        if (dgvDebugVariables is null)
        {
            return;
        }

        void Apply()
        {
            dgvDebugVariables.Rows.Clear();
            foreach (var variable in variables)
            {
                dgvDebugVariables.Rows.Add(variable.Name, variable.Value);
            }
        }

        if (dgvDebugVariables.InvokeRequired)
        {
            dgvDebugVariables.BeginInvoke(new Action(Apply));
            return;
        }

        Apply();
    }

    private void ClearDebugVariablesGrid()
    {
        UpdateDebugVariablesGrid(Array.Empty<DebugVariableValue>());
    }

    private ContextMenuStrip CreateCompileErrorsContextMenu(DataGridView grid)
    {
        var menu = new ContextMenuStrip();

        var menuCopyRows = new ToolStripMenuItem("复制选中行");
        menuCopyRows.Click += (_, _) => CopySelectedCompileErrorRows();

        var menuCopyCells = new ToolStripMenuItem("复制选中单元格");
        menuCopyCells.Click += (_, _) => CopySelectedCompileErrorCells();

        menu.Opening += (_, _) =>
        {
            menuCopyRows.Enabled = grid.SelectedRows.Count > 0;
            menuCopyCells.Enabled = grid.GetCellCount(DataGridViewElementStates.Selected) > 0;
        };

        menu.Items.Add(menuCopyRows);
        menu.Items.Add(menuCopyCells);
        return menu;
    }

    private void CopySelectedCompileErrorRows()
    {
        if (dgvCompileErrors is null || dgvCompileErrors.SelectedRows.Count == 0)
        {
            return;
        }

        var rows = dgvCompileErrors.SelectedRows
            .Cast<DataGridViewRow>()
            .OrderBy(row => row.Index)
            .ToList();

        var lines = new List<string>();
        foreach (var row in rows)
        {
            var values = row.Cells
                .Cast<DataGridViewCell>()
                .Select(cell => cell.Value?.ToString() ?? string.Empty);
            lines.Add(string.Join('\t', values));
        }

        if (lines.Count == 0)
        {
            return;
        }

        try
        {
            Clipboard.SetText(string.Join(Environment.NewLine, lines));
        }
        catch
        {
            // Ignore clipboard busy failures.
        }
    }

    private void CopySelectedCompileErrorCells()
    {
        if (dgvCompileErrors is null)
        {
            return;
        }

        var selectedCells = dgvCompileErrors.SelectedCells
            .Cast<DataGridViewCell>()
            .OrderBy(cell => cell.RowIndex)
            .ThenBy(cell => cell.ColumnIndex)
            .ToList();

        if (selectedCells.Count == 0)
        {
            return;
        }

        var grouped = selectedCells
            .GroupBy(cell => cell.RowIndex)
            .OrderBy(group => group.Key);

        var lines = new List<string>();
        foreach (var group in grouped)
        {
            var values = group
                .OrderBy(cell => cell.ColumnIndex)
                .Select(cell => cell.Value?.ToString() ?? string.Empty);
            lines.Add(string.Join('\t', values));
        }

        try
        {
            Clipboard.SetText(string.Join(Environment.NewLine, lines));
        }
        catch
        {
            // Ignore clipboard busy failures.
        }
    }

    private void DgvCompileErrors_MouseDown(object? sender, MouseEventArgs e)
    {
        if (dgvCompileErrors is null || e.Button != MouseButtons.Right)
        {
            return;
        }

        var hit = dgvCompileErrors.HitTest(e.X, e.Y);
        if (hit.RowIndex < 0)
        {
            return;
        }

        var row = dgvCompileErrors.Rows[hit.RowIndex];
        if (!row.Selected)
        {
            dgvCompileErrors.ClearSelection();
            row.Selected = true;
            if (hit.ColumnIndex >= 0 && hit.ColumnIndex < dgvCompileErrors.Columns.Count)
            {
                dgvCompileErrors.CurrentCell = row.Cells[hit.ColumnIndex];
            }
        }
    }

    private void DgvCompileErrors_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || dgvCompileErrors is null)
        {
            return;
        }

        var row = dgvCompileErrors.Rows[e.RowIndex];
        var rawFilePath = row.Cells["columnFile"].Value?.ToString()?.Trim() ?? string.Empty;
        var filePath = ResolveDiagnosticFilePath(rawFilePath);
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return;
        }

        ShowFileInEditorPlaceholder(filePath);

        var lineText = row.Cells["columnLine"].Value?.ToString();
        var columnText = row.Cells["columnColumn"].Value?.ToString();
        _ = int.TryParse(lineText, out var lineNumber);
        _ = int.TryParse(columnText, out var columnNumber);

        if (lineNumber > 0)
        {
            GoToLineInEditor(lineNumber, Math.Max(0, columnNumber - 1));
        }
    }

    private string ResolveDiagnosticFilePath(string rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return string.Empty;
        }

        try
        {
            if (Path.IsPathRooted(rawPath))
            {
                return Path.GetFullPath(rawPath);
            }

            if (!string.IsNullOrWhiteSpace(lastBuiltSourcePath))
            {
                var sourceDirectory = Path.GetDirectoryName(lastBuiltSourcePath);
                if (!string.IsNullOrWhiteSpace(sourceDirectory))
                {
                    var candidate = Path.GetFullPath(Path.Combine(sourceDirectory, rawPath));
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }

            return Path.GetFullPath(rawPath);
        }
        catch
        {
            return rawPath;
        }
    }
}
