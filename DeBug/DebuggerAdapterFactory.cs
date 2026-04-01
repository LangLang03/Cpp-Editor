namespace C__Editor;

internal static class DebuggerAdapterFactory
{
    internal static IDebuggerAdapter Create(DebuggerKind kind)
    {
        return kind switch
        {
            DebuggerKind.Cdb => new CdbDebuggerAdapter(),
            DebuggerKind.Gdb => new GdbDebuggerAdapter(),
            DebuggerKind.Lldb => new LldbDebuggerAdapter(),
            _ => throw new NotSupportedException($"Unsupported debugger kind: {kind}")
        };
    }
}
