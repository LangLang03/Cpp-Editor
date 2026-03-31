namespace C__Editor;

using System.ComponentModel;

internal sealed class CodeStructureBrowser : UserControl
{
    private readonly TreeView treeView;
    private readonly ToolStrip toolStrip;
    private readonly Label lblStatus;
    private CodeStructureParseResult? currentResult;
    private string? currentFilePath;
    private CodeStructureSettings settings = CodeStructureSettings.CreateDefault();

    public event EventHandler<CodeElementEventArgs>? ElementDoubleClicked;
    public event EventHandler<CodeStructureSettingsEventArgs>? SettingsChanged;

    public CodeStructureBrowser()
    {
        // Set UserControl to fill parent
        Dock = DockStyle.Fill;

        treeView = new TreeView
        {
            Dock = DockStyle.Fill,
            ShowLines = true,
            ShowPlusMinus = true,
            HideSelection = false,
            FullRowSelect = true,
            ImageList = CreateImageList()
        };
        treeView.DoubleClick += TreeView_DoubleClick;
        treeView.NodeMouseClick += TreeView_NodeMouseClick;

        toolStrip = new ToolStrip
        {
            Dock = DockStyle.Top,
            GripStyle = ToolStripGripStyle.Hidden
        };
        toolStrip.Items.AddRange(new ToolStripItem[]
        {
            new ToolStripButton("刷新", null, (_, _) => RefreshStructure()) { ToolTipText = "刷新代码结构" },
            new ToolStripSeparator(),
            new ToolStripButton("展开", null, (_, _) => treeView.ExpandAll()) { ToolTipText = "展开所有节点" },
            new ToolStripButton("折叠", null, (_, _) => treeView.CollapseAll()) { ToolTipText = "折叠所有节点" },
            new ToolStripSeparator(),
            new ToolStripButton("设置", null, (_, _) => ShowSettings()) { ToolTipText = "显示设置" }
        });

        lblStatus = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 22,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(4, 0, 0, 0),
            Text = "就绪"
        };

        Controls.Add(treeView);
        Controls.Add(toolStrip);
        Controls.Add(lblStatus);

        BorderStyle = BorderStyle.None;
    }

    public void SetSettings(CodeStructureSettings newSettings)
    {
        settings = newSettings.Clone();
        if (currentResult != null)
        {
            DisplayResult(currentResult);
        }
    }

    public void LoadFile(string filePath)
    {
        currentFilePath = filePath;
        RefreshStructure();
    }

    public void RefreshStructure()
    {
        if (string.IsNullOrWhiteSpace(currentFilePath))
        {
            treeView.Nodes.Clear();
            lblStatus.Text = "没有打开的文件";
            return;
        }

        lblStatus.Text = "正在解析...";
        Application.DoEvents();

        currentResult = CodeStructureParser.ParseFile(currentFilePath);
        DisplayResult(currentResult);
    }

    public void Clear()
    {
        treeView.Nodes.Clear();
        currentResult = null;
        currentFilePath = null;
        lblStatus.Text = "就绪";
    }

    private void DisplayResult(CodeStructureParseResult result)
    {
        treeView.Nodes.Clear();

        if (!string.IsNullOrWhiteSpace(result.ErrorMessage) && !result.IsPartial)
        {
            lblStatus.Text = $"错误: {result.ErrorMessage}";
            return;
        }

        var elements = FilterElements(result.Elements);
        
        foreach (var element in elements)
        {
            var node = CreateTreeNode(element);
            treeView.Nodes.Add(node);
            AddChildren(node, element);
        }

        lblStatus.Text = $"{elements.Count} 个顶级元素" + 
            (result.IsPartial ? $" (解析错误: {result.ErrorMessage})" : "");
    }

    private List<CodeElement> FilterElements(List<CodeElement> elements)
    {
        return elements.Where(e =>
        {
            return e.Type switch
            {
                CodeElementType.Include => settings.ShowIncludes,
                CodeElementType.Macro => settings.ShowMacros,
                CodeElementType.Variable or CodeElementType.Field => settings.ShowVariables,
                _ => true
            };
        }).ToList();
    }

    private TreeNode CreateTreeNode(CodeElement element)
    {
        var node = new TreeNode(element.DisplayText)
        {
            Tag = element,
            ImageKey = GetImageKey(element.Type),
            SelectedImageKey = GetImageKey(element.Type),
            ToolTipText = $"{element.Type} (第 {element.LineNumber} 行)"
        };
        return node;
    }

    private void AddChildren(TreeNode parentNode, CodeElement parentElement)
    {
        var children = FilterElements(parentElement.Children);
        
        foreach (var child in children)
        {
            var childNode = CreateTreeNode(child);
            parentNode.Nodes.Add(childNode);
            AddChildren(childNode, child);
        }
    }

    private void TreeView_DoubleClick(object? sender, EventArgs e)
    {
        if (treeView.SelectedNode?.Tag is CodeElement element)
        {
            ElementDoubleClicked?.Invoke(this, new CodeElementEventArgs(element));
        }
    }

    private void TreeView_NodeMouseClick(object? sender, TreeNodeMouseClickEventArgs e)
    {
        if (e.Button == MouseButtons.Right && e.Node?.Tag is CodeElement element)
        {
            ShowContextMenu(element, e.Location);
        }
    }

    private void ShowContextMenu(CodeElement element, Point location)
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add($"跳转到第 {element.LineNumber} 行", null, (_, _) =>
        {
            ElementDoubleClicked?.Invoke(this, new CodeElementEventArgs(element));
        });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add($"复制名称: {element.Name}", null, (_, _) =>
        {
            Clipboard.SetText(element.Name);
        });
        menu.Show(treeView, location);
    }

    private void ShowSettings()
    {
        var dialog = new CodeStructureSettingsForm(settings);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            settings = dialog.ResultSettings;
            SettingsChanged?.Invoke(this, new CodeStructureSettingsEventArgs(settings.Clone()));
            if (currentResult != null)
            {
                DisplayResult(currentResult);
            }
        }
    }

    private static ImageList CreateImageList()
    {
        var imageList = new ImageList();
        imageList.Images.Add("namespace", CreateColorBitmap(Color.Purple));
        imageList.Images.Add("class", CreateColorBitmap(Color.Blue));
        imageList.Images.Add("struct", CreateColorBitmap(Color.Cyan));
        imageList.Images.Add("enum", CreateColorBitmap(Color.Orange));
        imageList.Images.Add("function", CreateColorBitmap(Color.Green));
        imageList.Images.Add("method", CreateColorBitmap(Color.LightGreen));
        imageList.Images.Add("constructor", CreateColorBitmap(Color.Gold));
        imageList.Images.Add("destructor", CreateColorBitmap(Color.Red));
        imageList.Images.Add("variable", CreateColorBitmap(Color.Gray));
        imageList.Images.Add("field", CreateColorBitmap(Color.LightGray));
        imageList.Images.Add("typedef", CreateColorBitmap(Color.Magenta));
        imageList.Images.Add("using", CreateColorBitmap(Color.Teal));
        imageList.Images.Add("include", CreateColorBitmap(Color.DarkGray));
        imageList.Images.Add("macro", CreateColorBitmap(Color.Brown));
        imageList.Images.Add("template", CreateColorBitmap(Color.Indigo));
        return imageList;
    }

    private static Bitmap CreateColorBitmap(Color color)
    {
        var bmp = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bmp))
        {
            g.FillRectangle(new SolidBrush(color), 2, 2, 12, 12);
            g.DrawRectangle(Pens.Black, 2, 2, 11, 11);
        }
        return bmp;
    }

    private static string GetImageKey(CodeElementType type)
    {
        return type switch
        {
            CodeElementType.Namespace => "namespace",
            CodeElementType.Class => "class",
            CodeElementType.Struct => "struct",
            CodeElementType.Enum => "enum",
            CodeElementType.Function => "function",
            CodeElementType.Method => "method",
            CodeElementType.Constructor => "constructor",
            CodeElementType.Destructor => "destructor",
            CodeElementType.Variable => "variable",
            CodeElementType.Field => "field",
            CodeElementType.Typedef => "typedef",
            CodeElementType.Using => "using",
            CodeElementType.Include => "include",
            CodeElementType.Macro => "macro",
            CodeElementType.Template => "template",
            _ => "function"
        };
    }
}

internal sealed class CodeElementEventArgs : EventArgs
{
    public CodeElement Element { get; }

    public CodeElementEventArgs(CodeElement element)
    {
        Element = element;
    }
}

internal sealed class CodeStructureSettingsEventArgs : EventArgs
{
    public CodeStructureSettings Settings { get; }

    public CodeStructureSettingsEventArgs(CodeStructureSettings settings)
    {
        Settings = settings;
    }
}

internal sealed class CodeStructureSettingsForm : Form
{
    private readonly CheckBox chkShowIncludes;
    private readonly CheckBox chkShowMacros;
    private readonly CheckBox chkShowVariables;
    private readonly CheckBox chkSortAlphabetically;
    private readonly CheckBox chkAutoRefresh;

    public CodeStructureSettings ResultSettings { get; private set; }

    public CodeStructureSettingsForm(CodeStructureSettings currentSettings)
    {
        Text = "代码结构浏览器设置";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(360, 280);
        MinimumSize = new Size(360, 280);

        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16)
        };

        const int startY = 20;
        const int spacing = 32;
        
        chkShowIncludes = new CheckBox
        {
            Text = "显示 #include",
            Checked = currentSettings.ShowIncludes,
            AutoSize = true,
            Location = new Point(16, startY)
        };

        chkShowMacros = new CheckBox
        {
            Text = "显示宏定义 (#define)",
            Checked = currentSettings.ShowMacros,
            AutoSize = true,
            Location = new Point(16, startY + spacing)
        };

        chkShowVariables = new CheckBox
        {
            Text = "显示变量",
            Checked = currentSettings.ShowVariables,
            AutoSize = true,
            Location = new Point(16, startY + spacing * 2)
        };

        chkSortAlphabetically = new CheckBox
        {
            Text = "按字母排序",
            Checked = currentSettings.SortAlphabetically,
            AutoSize = true,
            Location = new Point(16, startY + spacing * 3)
        };

        chkAutoRefresh = new CheckBox
        {
            Text = "自动刷新",
            Checked = currentSettings.AutoRefresh,
            AutoSize = true,
            Location = new Point(16, startY + spacing * 4)
        };

        panel.Controls.AddRange(new Control[]
        {
            chkShowIncludes,
            chkShowMacros,
            chkShowVariables,
            chkSortAlphabetically,
            chkAutoRefresh
        });

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 50,
            Padding = new Padding(12, 8, 12, 8)
        };

        var btnOk = new Button
        {
            Text = "确定",
            DialogResult = DialogResult.OK,
            Width = 80,
            Height = 28,
            Margin = new Padding(0, 0, 8, 0)
        };

        var btnCancel = new Button
        {
            Text = "取消",
            DialogResult = DialogResult.Cancel,
            Width = 80,
            Height = 28,
            Margin = new Padding(0, 0, 0, 0)
        };

        buttonPanel.Controls.Add(btnCancel);
        buttonPanel.Controls.Add(btnOk);

        Controls.Add(panel);
        Controls.Add(buttonPanel);

        AcceptButton = btnOk;
        CancelButton = btnCancel;

        btnOk.Click += (_, _) =>
        {
            ResultSettings = new CodeStructureSettings
            {
                ShowIncludes = chkShowIncludes.Checked,
                ShowMacros = chkShowMacros.Checked,
                ShowVariables = chkShowVariables.Checked,
                SortAlphabetically = chkSortAlphabetically.Checked,
                AutoRefresh = chkAutoRefresh.Checked
            };
        };
    }
}
