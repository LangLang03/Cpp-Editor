namespace C__Editor;

internal sealed class CodeSnippetDialog : Form
{
    private readonly TreeView treeCategories;
    private readonly ListView listSnippets;
    private readonly TextBox txtPreview;
    private readonly TextBox txtSearch;
    private CodeSnippet? selectedSnippet;

    public CodeSnippet? SelectedSnippet => selectedSnippet;

    public CodeSnippetDialog()
    {
        Text = "插入代码片段";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        ClientSize = new Size(800, 600);
        MinimumSize = new Size(600, 400);

        // Search box
        txtSearch = new TextBox
        {
            Dock = DockStyle.Top,
            Height = 25,
            PlaceholderText = "搜索代码片段...",
            Margin = new Padding(8)
        };
        txtSearch.TextChanged += TxtSearch_TextChanged;

        // Split container
        var splitMain = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 200
        };

        // Categories tree
        treeCategories = new TreeView
        {
            Dock = DockStyle.Fill,
            ShowLines = true,
            HideSelection = false,
            FullRowSelect = true
        };
        treeCategories.AfterSelect += TreeCategories_AfterSelect;

        // Snippets list
        listSnippets = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            HideSelection = false,
            GridLines = true
        };
        listSnippets.Columns.Add("名称", 150);
        listSnippets.Columns.Add("快捷键", 80);
        listSnippets.Columns.Add("描述", 250);
        listSnippets.DoubleClick += ListSnippets_DoubleClick;
        listSnippets.SelectedIndexChanged += ListSnippets_SelectedIndexChanged;

        // Preview panel
        var splitRight = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 300
        };

        txtPreview = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            Font = new Font("Consolas", 10),
            ReadOnly = true,
            BackColor = SystemColors.Window
        };

        splitRight.Panel1.Controls.Add(listSnippets);
        splitRight.Panel2.Controls.Add(txtPreview);

        splitMain.Panel1.Controls.Add(treeCategories);
        splitMain.Panel2.Controls.Add(splitRight);

        // Buttons
        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 45,
            Padding = new Padding(8)
        };

        var btnOk = new Button
        {
            Text = "插入",
            DialogResult = DialogResult.OK,
            AutoSize = true,
            Enabled = false
        };

        var btnCancel = new Button
        {
            Text = "取消",
            DialogResult = DialogResult.Cancel,
            AutoSize = true
        };

        buttonPanel.Controls.Add(btnCancel);
        buttonPanel.Controls.Add(btnOk);

        // Layout
        var contentPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(8)
        };
        contentPanel.Controls.Add(splitMain);

        Controls.Add(contentPanel);
        Controls.Add(buttonPanel);
        Controls.Add(txtSearch);

        AcceptButton = btnOk;
        CancelButton = btnCancel;

        btnOk.Click += (_, _) =>
        {
            if (listSnippets.SelectedItems.Count > 0)
            {
                selectedSnippet = listSnippets.SelectedItems[0].Tag as CodeSnippet;
            }
        };

        Load += (_, _) => LoadSnippets();

        var themeId = EditorConfigurationController.GetUiSettings().ThemeId;
        EditorThemeController.ApplyFlatTheme(themeId, this);
    }

    private void LoadSnippets()
    {
        // Load categories
        treeCategories.Nodes.Clear();
        
        var rootNode = treeCategories.Nodes.Add("全部", "全部代码片段");
        
        foreach (SnippetCategory category in Enum.GetValues(typeof(SnippetCategory)))
        {
            if (category == SnippetCategory.Custom)
                continue;
                
            var node = treeCategories.Nodes.Add(category.ToString(), GetCategoryDisplayName(category));
            node.Tag = category;
        }
        
        treeCategories.SelectedNode = rootNode;
    }

    private void TreeCategories_AfterSelect(object? sender, TreeViewEventArgs e)
    {
        if (e.Node?.Tag is SnippetCategory category)
        {
            DisplaySnippets(CodeSnippetCatalog.GetSnippetsByCategory(category));
        }
        else
        {
            DisplaySnippets(CodeSnippetCatalog.GetBuiltInSnippets());
        }
    }

    private void TxtSearch_TextChanged(object? sender, EventArgs e)
    {
        var searchText = txtSearch.Text.Trim();
        if (string.IsNullOrWhiteSpace(searchText))
        {
            TreeCategories_AfterSelect(null, new TreeViewEventArgs(treeCategories.SelectedNode));
            return;
        }

        var allSnippets = CodeSnippetCatalog.GetBuiltInSnippets();
        var filtered = allSnippets.Where(s =>
            s.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
            s.Description.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
            s.Shortcut.Contains(searchText, StringComparison.OrdinalIgnoreCase)
        ).ToList();
        
        DisplaySnippets(filtered);
    }

    private void DisplaySnippets(IReadOnlyList<CodeSnippet> snippets)
    {
        listSnippets.Items.Clear();
        
        foreach (var snippet in snippets)
        {
            var item = new ListViewItem(new[]
            {
                snippet.Name,
                snippet.Shortcut,
                snippet.Description
            })
            {
                Tag = snippet
            };
            listSnippets.Items.Add(item);
        }

        if (listSnippets.Items.Count > 0)
        {
            listSnippets.Items[0].Selected = true;
        }
    }

    private void ListSnippets_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (listSnippets.SelectedItems.Count > 0 && listSnippets.SelectedItems[0].Tag is CodeSnippet snippet)
        {
            txtPreview.Text = snippet.Code;
            
            // Enable OK button
            if (Controls.Find("btnOk", true).FirstOrDefault() is Button btnOk)
            {
                btnOk.Enabled = true;
            }
        }
        else
        {
            txtPreview.Clear();
        }
    }

    private void ListSnippets_DoubleClick(object? sender, EventArgs e)
    {
        if (listSnippets.SelectedItems.Count > 0)
        {
            selectedSnippet = listSnippets.SelectedItems[0].Tag as CodeSnippet;
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    private static string GetCategoryDisplayName(SnippetCategory category)
    {
        return category switch
        {
            SnippetCategory.ControlFlow => "控制流程",
            SnippetCategory.Functions => "函数",
            SnippetCategory.Classes => "类与结构",
            SnippetCategory.Templates => "模板",
            SnippetCategory.Common => "常用代码",
            SnippetCategory.Custom => "自定义",
            _ => category.ToString()
        };
    }
}

internal sealed class CodeSnippetInsertService
{
    public static string ExpandSnippet(string code, Dictionary<string, string>? variables = null)
    {
        var result = code;
        
        // Replace common placeholders
        var defaultVariables = new Dictionary<string, string>
        {
            ["cursor"] = "",
            ["selected"] = "",
            ["clipboard"] = Clipboard.ContainsText() ? Clipboard.GetText() : ""
        };

        if (variables != null)
        {
            foreach (var pair in variables)
            {
                defaultVariables[pair.Key] = pair.Value;
            }
        }

        foreach (var pair in defaultVariables)
        {
            result = result.Replace($"${{{pair.Key}}}", pair.Value);
            result = result.Replace($"${pair.Key}", pair.Value);
        }

        return result;
    }

    public static int FindCursorPosition(string code)
    {
        // Find common cursor positions
        var cursorMarkers = new[] { "${cursor}", "$cursor", "|", "<|>" };
        
        foreach (var marker in cursorMarkers)
        {
            var pos = code.IndexOf(marker, StringComparison.Ordinal);
            if (pos >= 0)
            {
                return pos;
            }
        }

        // Find first empty line or indentation
        var lines = code.Split('\n');
        var charCount = 0;
        
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.TrimEnd();
            
            if (string.IsNullOrWhiteSpace(trimmed) && i > 0)
            {
                // Return position at the beginning of this empty line
                return charCount;
            }
            
            charCount += line.Length + 1; // +1 for newline
        }

        return code.Length;
    }
}
