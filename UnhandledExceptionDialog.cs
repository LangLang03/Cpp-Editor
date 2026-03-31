using System.Text;

namespace C__Editor;

internal sealed class UnhandledExceptionDialog : Form
{
    private const int CollapsedHeight = 245;
    private const int ExpandedHeight = 560;

    private readonly string detailText;
    private readonly Panel detailsPanel;
    private readonly Button btnToggleDetails;

    private bool detailsExpanded;

    internal UnhandledExceptionDialog(Exception exception, string source, bool isTerminating)
    {
        Text = "C++Editor - 未处理异常";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        TopMost = true;
        Width = 860;
        Height = CollapsedHeight;
        MinimumSize = new Size(860, CollapsedHeight);

        detailText = BuildDetailText(exception, source, isTerminating);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(14, 14, 14, 12)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        var headerPanel = BuildHeader(exception);
        root.Controls.Add(headerPanel, 0, 0);

        btnToggleDetails = new Button
        {
            Text = "展开堆栈详情 ▼",
            AutoSize = true,
            Margin = new Padding(0, 8, 0, 6)
        };
        btnToggleDetails.Click += (_, _) => ToggleDetails();
        root.Controls.Add(btnToggleDetails, 0, 1);

        detailsPanel = BuildDetailsPanel(detailText);
        detailsPanel.Visible = false;
        root.Controls.Add(detailsPanel, 0, 2);

        var buttonPanel = BuildButtonPanel();
        root.Controls.Add(buttonPanel, 0, 3);
    }

    private Control BuildHeader(Exception exception)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        var iconBox = new PictureBox
        {
            SizeMode = PictureBoxSizeMode.CenterImage,
            Size = new Size(48, 48),
            Margin = new Padding(0, 2, 12, 0),
            Image = SystemIcons.Error.ToBitmap()
        };
        panel.Controls.Add(iconBox, 0, 0);

        var textPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Dock = DockStyle.Fill,
            AutoSize = true
        };

        var titleLabel = new Label
        {
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold),
            Text = "程序发生未处理异常，应用将退出。"
        };

        var messageLabel = new Label
        {
            AutoSize = false,
            Width = 740,
            Height = 70,
            Font = new Font("Microsoft YaHei UI", 9.5f),
            Text = $"异常信息: {exception.Message}"
        };

        textPanel.Controls.Add(titleLabel);
        textPanel.Controls.Add(messageLabel);
        panel.Controls.Add(textPanel, 1, 0);

        return panel;
    }

    private static Panel BuildDetailsPanel(string details)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 8)
        };

        var detailsBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Font = new Font("Consolas", 9f),
            Text = details
        };

        panel.Controls.Add(detailsBox);
        return panel;
    }

    private Control BuildButtonPanel()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Margin = new Padding(0, 8, 0, 0)
        };

        var btnExit = new Button
        {
            Text = "退出",
            AutoSize = true
        };
        btnExit.Click += (_, _) =>
        {
            DialogResult = DialogResult.OK;
            Close();
        };

        var btnCopyAndExit = new Button
        {
            Text = "复制并退出",
            AutoSize = true
        };
        btnCopyAndExit.Click += (_, _) =>
        {
            TryCopyDetailText();
            DialogResult = DialogResult.Abort;
            Close();
        };

        panel.Controls.Add(btnExit);
        panel.Controls.Add(btnCopyAndExit);

        AcceptButton = btnExit;
        CancelButton = btnExit;
        return panel;
    }

    private void ToggleDetails()
    {
        detailsExpanded = !detailsExpanded;
        detailsPanel.Visible = detailsExpanded;
        btnToggleDetails.Text = detailsExpanded ? "收起堆栈详情 ▲" : "展开堆栈详情 ▼";
        Height = detailsExpanded ? ExpandedHeight : CollapsedHeight;
    }

    private void TryCopyDetailText()
    {
        try
        {
            Clipboard.SetText(detailText);
        }
        catch
        {
            // Ignore clipboard failures and still exit.
        }
    }

    private static string BuildDetailText(Exception exception, string source, bool isTerminating)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
        builder.AppendLine($"来源: {source}");
        builder.AppendLine($"即将终止: {(isTerminating ? "是" : "否")}");
        builder.AppendLine();
        AppendException(builder, exception, depth: 0);
        return builder.ToString();
    }

    private static void AppendException(StringBuilder builder, Exception exception, int depth)
    {
        var indent = new string(' ', depth * 2);
        builder.AppendLine($"{indent}异常类型: {exception.GetType().FullName}");
        builder.AppendLine($"{indent}消息: {exception.Message}");
        builder.AppendLine($"{indent}堆栈:");
        builder.AppendLine(exception.StackTrace ?? $"{indent}<无堆栈>");
        builder.AppendLine();

        if (exception.InnerException is not null)
        {
            builder.AppendLine($"{indent}内部异常:");
            AppendException(builder, exception.InnerException, depth + 1);
        }
    }
}
