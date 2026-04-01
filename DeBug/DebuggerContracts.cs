namespace C__Editor;

internal enum DebuggerKind
{
    Cdb,
    Gdb,
    Lldb
}

internal enum DebugExecutionState
{
    Paused,
    Exited,
    Unknown
}

internal enum DebugStopReason
{
    Breakpoint,
    StepComplete,
    Exception,
    Entry,
    Unknown
}

internal sealed class DebugVariableValue
{
    public string Name { get; init; } = string.Empty;

    public string Value { get; init; } = string.Empty;
}

internal sealed class DebugPauseSnapshot
{
    public string FilePath { get; init; } = string.Empty;

    public int Line { get; init; }

    public int Column { get; init; }

    public string FunctionName { get; init; } = string.Empty;

    public DebugStopReason Reason { get; init; } = DebugStopReason.Unknown;

    public bool CanStepOut { get; init; }

    public IReadOnlyList<DebugVariableValue> Variables { get; init; } = Array.Empty<DebugVariableValue>();

    public string RawOutput { get; init; } = string.Empty;
}

internal sealed class DebugCommandResult
{
    public DebugExecutionState State { get; init; } = DebugExecutionState.Unknown;

    public DebugPauseSnapshot? Pause { get; init; }

    public int? ExitCode { get; init; }

    public string RawOutput { get; init; } = string.Empty;
}

internal sealed class DebugLaunchRequest
{
    public string ExecutablePath { get; init; } = string.Empty;

    public string WorkingDirectory { get; init; } = string.Empty;

    public IReadOnlyDictionary<string, IReadOnlyCollection<int>> BreakpointsByFile { get; init; } =
        new Dictionary<string, IReadOnlyCollection<int>>(StringComparer.OrdinalIgnoreCase);
}

internal sealed class DebuggerResolution
{
    public DebuggerKind Kind { get; init; } = DebuggerKind.Gdb;

    public string ExecutablePath { get; init; } = string.Empty;

    public string Detail { get; init; } = string.Empty;
}
