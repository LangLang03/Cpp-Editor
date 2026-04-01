namespace C__Editor;

internal sealed class DebugControlPopup : Form
{
    private readonly Button btnContinue;
    private readonly Button btnStepInto;
    private readonly Button btnStepOver;
    private readonly Button btnStepOut;
    private readonly Button btnStop;

    internal event EventHandler? ContinueRequested;
    internal event EventHandler? StepIntoRequested;
    internal event EventHandler? StepOverRequested;
    internal event EventHandler? StepOutRequested;
    internal event EventHandler? StopRequested;

    internal DebugControlPopup()
    {
        Text = "调试控制";
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        MaximizeBox = false;
        MinimizeBox = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        MinimumSize = new Size(760, 130);
        Size = new Size(920, 140);

        var layout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(10),
            Margin = new Padding(0)
        };

        btnContinue = CreateButton("继续");
        btnStepInto = CreateButton("单步进入");
        btnStepOver = CreateButton("单步越过");
        btnStepOut = CreateButton("单步跳出");
        btnStop = CreateButton("停止");

        btnContinue.Click += (_, _) => ContinueRequested?.Invoke(this, EventArgs.Empty);
        btnStepInto.Click += (_, _) => StepIntoRequested?.Invoke(this, EventArgs.Empty);
        btnStepOver.Click += (_, _) => StepOverRequested?.Invoke(this, EventArgs.Empty);
        btnStepOut.Click += (_, _) => StepOutRequested?.Invoke(this, EventArgs.Empty);
        btnStop.Click += (_, _) => StopRequested?.Invoke(this, EventArgs.Empty);

        layout.Controls.Add(btnContinue);
        layout.Controls.Add(btnStepInto);
        layout.Controls.Add(btnStepOver);
        layout.Controls.Add(btnStepOut);
        layout.Controls.Add(btnStop);
        Controls.Add(layout);
    }

    internal void UpdateState(bool canContinue, bool canStepInto, bool canStepOver, bool canStepOut, bool canStop)
    {
        btnContinue.Enabled = canContinue;
        btnStepInto.Enabled = canStepInto;
        btnStepOver.Enabled = canStepOver;
        btnStepOut.Enabled = canStepOut;
        btnStop.Enabled = canStop;
    }

    private static Button CreateButton(string text)
    {
        return new Button
        {
            Text = text,
            AutoSize = false,
            Size = new Size(150, 44),
            Margin = new Padding(6),
            MinimumSize = new Size(120, 40)
        };
    }
}
